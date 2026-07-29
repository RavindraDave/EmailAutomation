using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Templates;
using Moq;
using Xunit;

namespace EmailAutomation.Tests;

public class ScribanTemplateEngineTests
{
    [Fact]
    public void Render_ReplacesPlaceholdersCorrectly()
    {
        // Arrange
        var engine = new ScribanTemplateEngine();
        var template = "Hello {{name}}, your invoice number is {{invoice_no}}.";
        var variables = new Dictionary<string, string>
        {
            { "name", "John Doe" },
            { "invoice_no", "INV-12345" }
        };

        // Act
        var result = engine.Render(template, variables);

        // Assert
        Assert.Equal("Hello John Doe, your invoice number is INV-12345.", result);
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenTemplateIsNullOrEmpty()
    {
        var engine = new ScribanTemplateEngine();
        var result = engine.Render("", new Dictionary<string, string>());
        Assert.Equal(string.Empty, result);
    }
}

public class BatchExecutionServiceTests
{
    [Fact]
    public async Task ExecuteBatchAsync_SendsEmailForEachJob()
    {
        // Arrange
        var mockExcelReader = new Mock<IExcelReader>();
        var jobs = new List<EmailJob>
        {
            new EmailJob { To = "test1@test.com", Subject = "Subject 1", Variables = new Dictionary<string, string>() },
            new EmailJob { To = "test2@test.com", Subject = "Subject 2", Variables = new Dictionary<string, string>() }
        };
        mockExcelReader.Setup(x => x.ReadJobs(It.IsAny<string>())).Returns(jobs);

        var mockTemplateEngine = new Mock<ITemplateEngine>();
        mockTemplateEngine.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((string tmpl, Dictionary<string, string> vars) => tmpl + "_rendered");

        var mockEmailSender = new Mock<IEmailSender>();
        mockEmailSender.Setup(x => x.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new SendResult { Success = true });

        var mockSenderFactory = new Mock<IEmailSenderFactory>();
        mockSenderFactory.Setup(x => x.Create(It.IsAny<AppSettings>())).Returns(mockEmailSender.Object);

        var mockSettingsService = new Mock<ISettingsService>();
        // DelayBetweenSendsMs = 0 keeps this test fast - throttle timing has its own dedicated test.
        mockSettingsService.Setup(x => x.LoadAsync()).ReturnsAsync(new AppSettings { DelayBetweenSendsMs = 0 });

        var mockRepository = new Mock<IRepository>();
        mockRepository.Setup(r => r.CreateBatchRunAsync(It.IsAny<BatchRun>())).ReturnsAsync(Guid.NewGuid());
        mockRepository.Setup(r => r.CountSentSinceAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
        mockRepository.Setup(r => r.UpsertEmailLogAsync(It.IsAny<EmailLog>())).Returns(Task.CompletedTask);
        mockRepository.Setup(r => r.GetLogsForBatchAsync(It.IsAny<Guid>())).ReturnsAsync(new List<EmailLog>
        {
            new() { Status = EmailLogStatus.Sent },
            new() { Status = EmailLogStatus.Sent },
        });
        mockRepository
            .Setup(r => r.CompleteBatchRunAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var service = new BatchExecutionService(mockExcelReader.Object, mockTemplateEngine.Object, mockSenderFactory.Object, mockSettingsService.Object, mockRepository.Object);
        var template = new EmailTemplate { SubjectTemplate = "SubjectTmpl", BodyTemplate = "BodyTmpl" };

        // Act
        var request = new BatchRequest { ExcelFilePath = "dummy.xlsx", Template = template };
        var summary = await service.ExecuteBatchAsync(request);

        // Assert
        mockEmailSender.Verify(x => x.SendEmailAsync(
            It.Is<EmailJob>(j => j.To == "test1@test.com"),
            "Subject 1_rendered",
            "BodyTmpl_rendered"
        ), Times.Once);

        mockEmailSender.Verify(x => x.SendEmailAsync(
            It.Is<EmailJob>(j => j.To == "test2@test.com"),
            "Subject 2_rendered",
            "BodyTmpl_rendered"
        ), Times.Once);

        Assert.Equal(BatchRunStatus.Completed, summary.Status);
    }
}
