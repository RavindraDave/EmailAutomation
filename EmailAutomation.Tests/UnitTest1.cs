using System;
using System.Collections.Generic;
using System.Threading;
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

        var service = new BatchExecutionService(mockExcelReader.Object, mockTemplateEngine.Object, mockEmailSender.Object);
        var template = new EmailTemplate { SubjectTemplate = "SubjectTmpl", BodyTemplate = "BodyTmpl" };

        // Act
        await service.ExecuteBatchAsync("dummy.xlsx", template, CancellationToken.None);

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
    }
}
