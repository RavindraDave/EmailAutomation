using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Email;
using EmailAutomation.Infrastructure.Templates;
using Moq;
using Xunit;

namespace EmailAutomation.Tests;

public class BatchValidationServiceTests
{
    // Real Scriban engine and real MimeKit-backed address validator - this exercises the actual
    // regex/Scriban/MimeKit interaction, not just mocked behavior.
    private static BatchValidationService CreateService(IEnumerable<EmailJob> jobs)
    {
        var excelReader = new Mock<IExcelReader>();
        excelReader.Setup(x => x.ReadJobs(It.IsAny<string>())).Returns(jobs);

        return new BatchValidationService(excelReader.Object, new ScribanTemplateEngine(), new MimeKitEmailAddressValidator());
    }

    [Fact]
    public void Validate_FlagsInvalidEmailAddress()
    {
        var jobs = new List<EmailJob>
        {
            new() { RowNumber = 2, To = "not-an-email", Variables = new Dictionary<string, string>() },
        };
        var template = new EmailTemplate { SubjectTemplate = "Hi", BodyTemplate = "Hello" };

        var report = CreateService(jobs).Validate("dummy.xlsx", template);

        Assert.True(report.HasBlockingErrors);
        Assert.Contains(report.Issues, i => i.RowNumber == 2 && i.Message.Contains("not-an-email"));
        Assert.Equal(0, report.ValidRows);
    }

    [Fact]
    public void Validate_FlagsMissingAttachment()
    {
        var jobs = new List<EmailJob>
        {
            new() { RowNumber = 1, To = "a@example.com", AttachmentPath = "/nonexistent/path/file.pdf", Variables = new Dictionary<string, string>() },
        };
        var template = new EmailTemplate { SubjectTemplate = "Hi", BodyTemplate = "Hello" };

        var report = CreateService(jobs).Validate("dummy.xlsx", template);

        Assert.True(report.HasBlockingErrors);
        Assert.Contains(report.Issues, i => i.RowNumber == 1 && i.Message.Contains("Attachment not found"));
    }

    [Fact]
    public void Validate_FlagsPlaceholderWithNoMatchingColumn()
    {
        var jobs = new List<EmailJob>
        {
            new() { RowNumber = 1, To = "a@example.com", Variables = new Dictionary<string, string> { { "FirstName", "Ann" } } },
        };
        var template = new EmailTemplate { SubjectTemplate = "Hi {{FirstName}}", BodyTemplate = "Your code is {{InvoiceNo}}" };

        var report = CreateService(jobs).Validate("dummy.xlsx", template);

        Assert.Contains("InvoiceNo", report.UnmatchedPlaceholders);
        Assert.DoesNotContain("FirstName", report.UnmatchedPlaceholders);
        Assert.True(report.HasBlockingErrors);
    }

    [Fact]
    public void Validate_FlagsTemplateSyntaxError()
    {
        var jobs = new List<EmailJob>
        {
            new() { RowNumber = 1, To = "a@example.com", Variables = new Dictionary<string, string>() },
        };
        // Unclosed Scriban tag - a real syntax error, not just a missing variable.
        var template = new EmailTemplate { SubjectTemplate = "Hi", BodyTemplate = "{{ if true " };

        var report = CreateService(jobs).Validate("dummy.xlsx", template);

        Assert.True(report.HasBlockingErrors);
        Assert.Contains(report.Issues, i => i.RowNumber == 1 && i.Message.Contains("render"));
    }

    [Fact]
    public void Validate_CleanBatch_HasNoBlockingErrorsAndPopulatesPreviews()
    {
        var jobs = Enumerable.Range(1, 3)
            .Select(i => new EmailJob { RowNumber = i, To = $"user{i}@example.com", Variables = new Dictionary<string, string> { { "FirstName", $"User{i}" } } })
            .ToList();
        var template = new EmailTemplate { SubjectTemplate = "Hi {{FirstName}}", BodyTemplate = "Welcome, {{FirstName}}!" };

        var report = CreateService(jobs).Validate("dummy.xlsx", template, previewRows: 2);

        Assert.False(report.HasBlockingErrors);
        Assert.Equal(3, report.TotalRows);
        Assert.Equal(3, report.ValidRows);
        Assert.Equal(2, report.Previews.Count); // capped at previewRows even though there are 3 valid rows
        Assert.Equal("Hi User1", report.Previews[0].RenderedSubject);
        Assert.Equal("Welcome, User1!", report.Previews[0].RenderedBody);
    }

    [Fact]
    public void Validate_NoRows_ReportsBlockingError()
    {
        var report = CreateService(new List<EmailJob>()).Validate("dummy.xlsx", new EmailTemplate());

        Assert.True(report.HasBlockingErrors);
        Assert.Equal(0, report.TotalRows);
    }

    [Fact]
    public void Validate_ExcelReadFailure_ReportsBlockingErrorInstead_OfThrowing()
    {
        var excelReader = new Mock<IExcelReader>();
        excelReader.Setup(x => x.ReadJobs(It.IsAny<string>())).Throws(new FileNotFoundException("nope"));
        var service = new BatchValidationService(excelReader.Object, new ScribanTemplateEngine(), new MimeKitEmailAddressValidator());

        var report = service.Validate("missing.xlsx", new EmailTemplate());

        Assert.True(report.HasBlockingErrors);
        Assert.Contains(report.Issues, i => i.Message.Contains("Could not read"));
    }
}
