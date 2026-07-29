using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EmailAutomation.Application.Reporting;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Database;
using EmailAutomation.Infrastructure.Email;
using EmailAutomation.Infrastructure.Excel;
using EmailAutomation.Infrastructure.Templates;
using Moq;
using Xunit;

namespace EmailAutomation.Tests;

/// <summary>
/// Walks the same path a real user would from the UI - prepare a spreadsheet with deliberate
/// mistakes, validate, fix, send, stop mid-batch, resume, and export a report - but against real
/// infrastructure (an actual SQLite file, an actual .xlsx, the real Scriban engine) instead of
/// mocks, and with a mocked IEmailSender standing in for an actual mail server. This is the
/// closest automated equivalent to the plan's manual end-to-end smoke test.
/// </summary>
public class EndToEndSmokeTests
{
    [Fact]
    public async Task FullLifecycle_ValidateFixSendStopResumeExport_WorksAgainstRealInfrastructure()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"e2e_{Guid.NewGuid():N}.db");
        var excelPath = Path.Combine(Path.GetTempPath(), $"e2e_{Guid.NewGuid():N}.xlsx");
        var missingAttachmentPath = Path.Combine(Path.GetTempPath(), $"e2e_missing_{Guid.NewGuid():N}.pdf");
        try
        {
            var connStr = $"Data Source={dbPath}";
            new DatabaseInitializer(connStr).Initialize();
            IRepository repository = new SqliteRepository(connStr);
            IExcelReader excelReader = new ClosedXmlExcelReader();
            ITemplateEngine templateEngine = new ScribanTemplateEngine();
            IEmailAddressValidator addressValidator = new MimeKitEmailAddressValidator();
            var validationService = new BatchValidationService(excelReader, templateEngine, addressValidator);

            // --- Step 1: a spreadsheet with deliberate mistakes, like a real user's first attempt ---
            WriteWorkbook(excelPath, ws =>
            {
                ws.Cell(1, 1).Value = "To";
                ws.Cell(1, 2).Value = "AttachmentPath";
                ws.Cell(1, 3).Value = "FirstName";
                ws.Cell(2, 1).Value = "alice@example.com";
                ws.Cell(2, 2).Value = "";
                ws.Cell(2, 3).Value = "Alice";
                ws.Cell(3, 1).Value = "not-an-email"; // mistake #1: invalid address
                ws.Cell(3, 3).Value = "Bob";
                ws.Cell(4, 1).Value = "carol@example.com";
                ws.Cell(4, 2).Value = missingAttachmentPath; // mistake #2: attachment doesn't exist
                ws.Cell(4, 3).Value = "Carol";
            });

            var brokenTemplate = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                SubjectTemplate = "Hi {{FirstName}}",
                BodyTemplate = "Your invoice number is {{InvoiceNo}}", // mistake #3: no such column
            };

            var firstReport = validationService.Validate(excelPath, brokenTemplate);
            Assert.True(firstReport.HasBlockingErrors);
            Assert.Contains(firstReport.Issues, i => i.Message.Contains("not-an-email"));
            Assert.Contains(firstReport.Issues, i => i.Message.Contains("Attachment not found"));
            Assert.Contains("InvoiceNo", firstReport.UnmatchedPlaceholders);

            // --- Step 2: fix the mistakes, exactly as the UI's guidance would lead a user to ---
            WriteWorkbook(excelPath, ws =>
            {
                ws.Cell(1, 1).Value = "To";
                ws.Cell(1, 2).Value = "FirstName";
                ws.Cell(2, 1).Value = "alice@example.com";
                ws.Cell(2, 2).Value = "Alice";
                ws.Cell(3, 1).Value = "bob@example.com";
                ws.Cell(3, 2).Value = "Bob";
                ws.Cell(4, 1).Value = "carol@example.com";
                ws.Cell(4, 2).Value = "Carol";
            });
            var fixedTemplate = new EmailTemplate { Id = Guid.NewGuid(), SubjectTemplate = "Hi {{FirstName}}", BodyTemplate = "Welcome, {{FirstName}}!" };

            var secondReport = validationService.Validate(excelPath, fixedTemplate);
            Assert.False(secondReport.HasBlockingErrors);
            Assert.Equal(3, secondReport.ValidRows);

            // --- Step 3: send, but stop after 2 of 3 rows (simulating the user clicking Stop) ---
            var sendCallCount = 0;
            var mockSender = new Mock<IEmailSender>();
            mockSender
                .Setup(s => s.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new SendResult { Success = true, Attempts = 1 })
                .Callback(() => sendCallCount++);

            var mockSenderFactory = new Mock<IEmailSenderFactory>();
            mockSenderFactory.Setup(f => f.Create(It.IsAny<AppSettings>())).Returns(mockSender.Object);

            var mockSettingsService = new Mock<ISettingsService>();
            mockSettingsService.Setup(s => s.LoadAsync()).ReturnsAsync(new AppSettings { DelayBetweenSendsMs = 0 });

            var batchService = new BatchExecutionService(excelReader, templateEngine, mockSenderFactory.Object, mockSettingsService.Object, repository);

            using (var cts = new System.Threading.CancellationTokenSource())
            {
                mockSender.Setup(s => s.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SendResult { Success = true, Attempts = 1 })
                    .Callback(() =>
                    {
                        sendCallCount++;
                        if (sendCallCount == 2)
                        {
                            cts.Cancel(); // stop after the 2nd row, like clicking Stop mid-batch
                        }
                    });

                var request = new BatchRequest { ExcelFilePath = excelPath, Template = fixedTemplate };
                var firstRunSummary = await batchService.ExecuteBatchAsync(request, cancellationToken: cts.Token);

                Assert.Equal(BatchRunStatus.Cancelled, firstRunSummary.Status);
                Assert.Equal(2, firstRunSummary.SentCount);

                // --- Step 4: relaunching the app should offer to resume this exact run ---
                var resumable = await repository.FindResumableRunAsync(excelPath, fixedTemplate.Id);
                Assert.NotNull(resumable);
                Assert.Equal(firstRunSummary.BatchId, resumable!.Id);

                // --- Step 5: resume - only the un-sent row should actually be sent again ---
                sendCallCount = 0;
                var resumeRequest = new BatchRequest { ExcelFilePath = excelPath, Template = fixedTemplate, ResumeBatchId = resumable.Id };
                var resumedSummary = await batchService.ExecuteBatchAsync(resumeRequest);

                Assert.Equal(BatchRunStatus.Completed, resumedSummary.Status);
                Assert.Equal(3, resumedSummary.SentCount); // 2 already-sent (real DB) + 1 new
                Assert.Equal(2, resumedSummary.SkippedCount); // alice and bob were both already sent
                Assert.Equal(1, sendCallCount); // only carol (the row that hadn't succeeded yet) was actually sent
            }

            // --- Step 6: Dashboard-equivalent checks against the real database ---
            var (totalSuccess, totalFailure) = await repository.GetOverallEmailCountsAsync();
            Assert.Equal(3, totalSuccess);
            Assert.Equal(0, totalFailure);

            var recentRuns = (await repository.GetRecentRunsAsync(10)).ToList();
            Assert.Single(recentRuns);
            Assert.Equal(BatchRunStatus.Completed, recentRuns[0].Status);

            // --- Step 7: export report - must be readable back as valid CSV with the real rows ---
            var logs = await repository.GetLogsForBatchAsync(recentRuns[0].Id);
            await using var csvWriter = new StringWriter();
            CsvReportWriter.WriteReport(logs, csvWriter);
            var csvLines = csvWriter.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, csvLines.Length); // header + 3 recipients
            Assert.Contains(csvLines, l => l.Contains("alice@example.com"));
            Assert.Contains(csvLines, l => l.Contains("bob@example.com"));
            Assert.Contains(csvLines, l => l.Contains("carol@example.com"));
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(excelPath)) File.Delete(excelPath);
        }
    }

    private static void WriteWorkbook(string path, Action<IXLWorksheet> populate)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Recipients");
        populate(worksheet);
        workbook.SaveAs(path);
    }
}
