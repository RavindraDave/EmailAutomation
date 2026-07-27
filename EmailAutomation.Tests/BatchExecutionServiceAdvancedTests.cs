using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EmailAutomation.Tests;

public class BatchExecutionServiceAdvancedTests
{
    private sealed class Harness
    {
        public required Mock<IExcelReader> ExcelReader { get; init; }
        public required Mock<IEmailSender> EmailSender { get; init; }
        public required Mock<ISettingsService> SettingsService { get; init; }
        public required Mock<IRepository> Repository { get; init; }
        public required BatchExecutionService Service { get; init; }
        public required EmailTemplate Template { get; init; }
    }

    private static Harness CreateHarness(List<EmailJob> jobs, AppSettings settings, TimeProvider? timeProvider = null, Guid? existingBatchId = null)
    {
        var excelReader = new Mock<IExcelReader>();
        excelReader.Setup(x => x.ReadJobs(It.IsAny<string>())).Returns(jobs);

        var templateEngine = new Mock<ITemplateEngine>();
        templateEngine
            .Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((string t, Dictionary<string, string> _) => t);

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(x => x.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new SendResult { Success = true, Attempts = 1 });

        var senderFactory = new Mock<IEmailSenderFactory>();
        senderFactory.Setup(x => x.Create(It.IsAny<AppSettings>())).Returns(emailSender.Object);

        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(x => x.LoadAsync()).ReturnsAsync(settings);

        var repository = new Mock<IRepository>();
        repository.Setup(r => r.CreateBatchRunAsync(It.IsAny<BatchRun>())).ReturnsAsync(existingBatchId ?? Guid.NewGuid());
        repository.Setup(r => r.CountSentSinceAsync(It.IsAny<DateTime>())).ReturnsAsync(0);
        repository.Setup(r => r.UpsertEmailLogAsync(It.IsAny<EmailLog>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.GetSentRowNumbersAsync(It.IsAny<Guid>())).ReturnsAsync(new HashSet<int>());
        repository
            .Setup(r => r.CompleteBatchRunAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetLogsForBatchAsync(It.IsAny<Guid>())).ReturnsAsync(new List<EmailLog>());

        var template = new EmailTemplate { Id = Guid.NewGuid(), SubjectTemplate = "Subject", BodyTemplate = "Body" };

        var service = new BatchExecutionService(
            excelReader.Object, templateEngine.Object, senderFactory.Object, settingsService.Object, repository.Object, timeProvider);

        return new Harness
        {
            ExcelReader = excelReader,
            EmailSender = emailSender,
            SettingsService = settingsService,
            Repository = repository,
            Service = service,
            Template = template,
        };
    }

    private static List<EmailJob> TwoJobs() => new()
    {
        new EmailJob { RowNumber = 1, To = "a@test.com", Variables = new Dictionary<string, string>() },
        new EmailJob { RowNumber = 2, To = "b@test.com", Variables = new Dictionary<string, string>() },
    };

    [Fact]
    public async Task ExecuteBatchAsync_ThrottlesBetweenSends_UsingConfiguredDelay()
    {
        var fakeTime = new FakeTimeProvider();
        var harness = CreateHarness(TwoJobs(), new AppSettings { DelayBetweenSendsMs = 5000 }, fakeTime);
        harness.Repository
            .Setup(r => r.GetLogsForBatchAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<EmailLog> { new() { Status = EmailLogStatus.Sent }, new() { Status = EmailLogStatus.Sent } });

        var request = new BatchRequest { ExcelFilePath = "dummy.xlsx", Template = harness.Template };
        var executeTask = harness.Service.ExecuteBatchAsync(request);

        // Every mocked dependency completes synchronously, so the only real suspension points are
        // the throttle delays - advancing virtual time (not real time) is what lets the loop proceed.
        fakeTime.Advance(TimeSpan.FromMilliseconds(5000));
        fakeTime.Advance(TimeSpan.FromMilliseconds(5000));

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(executeTask, completed); // fails fast with a clear signal if a delay never unblocked

        var summary = await executeTask;
        Assert.Equal(2, summary.SentCount);
        harness.EmailSender.Verify(x => x.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteBatchAsync_PausesUntilResumed()
    {
        var harness = CreateHarness(TwoJobs(), new AppSettings { DelayBetweenSendsMs = 0 });
        harness.Repository
            .Setup(r => r.GetLogsForBatchAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<EmailLog> { new() { Status = EmailLogStatus.Sent }, new() { Status = EmailLogStatus.Sent } });

        var pauseTokenSource = new PauseTokenSource();
        pauseTokenSource.Pause();

        var request = new BatchRequest { ExcelFilePath = "dummy.xlsx", Template = harness.Template };
        var executeTask = harness.Service.ExecuteBatchAsync(request, progress: null, pauseTokenSource.Token);

        await Task.Delay(50); // let the loop actually reach and block on the pause gate
        Assert.False(executeTask.IsCompleted);
        harness.EmailSender.Verify(x => x.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        pauseTokenSource.Resume();

        var summary = await executeTask;
        Assert.Equal(BatchRunStatus.Completed, summary.Status);
        Assert.Equal(2, summary.SentCount);
    }

    [Fact]
    public async Task ExecuteBatchAsync_CancelledBeforeStart_MarksRunCancelled_AndSendsNothing()
    {
        var harness = CreateHarness(TwoJobs(), new AppSettings { DelayBetweenSendsMs = 0 });

        string? capturedStatus = null;
        harness.Repository
            .Setup(r => r.CompleteBatchRunAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<Guid, string, DateTime, int, int, int>((_, status, _, _, _, _) => capturedStatus = status)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new BatchRequest { ExcelFilePath = "dummy.xlsx", Template = harness.Template };
        var summary = await harness.Service.ExecuteBatchAsync(request, cancellationToken: cts.Token);

        Assert.Equal(BatchRunStatus.Cancelled, summary.Status);
        Assert.Equal(BatchRunStatus.Cancelled, capturedStatus);
        harness.EmailSender.Verify(x => x.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteBatchAsync_Resume_SkipsRowsAlreadySent()
    {
        var existingBatchId = Guid.NewGuid();
        var harness = CreateHarness(TwoJobs(), new AppSettings { DelayBetweenSendsMs = 0 }, existingBatchId: existingBatchId);
        harness.Repository.Setup(r => r.GetSentRowNumbersAsync(existingBatchId)).ReturnsAsync(new HashSet<int> { 1 });
        harness.Repository
            .Setup(r => r.GetLogsForBatchAsync(existingBatchId))
            .ReturnsAsync(new List<EmailLog> { new() { RowNumber = 1, Status = EmailLogStatus.Sent }, new() { RowNumber = 2, Status = EmailLogStatus.Sent } });

        var request = new BatchRequest { ExcelFilePath = "dummy.xlsx", Template = harness.Template, ResumeBatchId = existingBatchId };
        var summary = await harness.Service.ExecuteBatchAsync(request);

        Assert.Equal(existingBatchId, summary.BatchId);
        Assert.Equal(1, summary.SkippedCount);
        harness.EmailSender.Verify(x => x.SendEmailAsync(It.Is<EmailJob>(j => j.To == "a@test.com"), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        harness.EmailSender.Verify(x => x.SendEmailAsync(It.Is<EmailJob>(j => j.To == "b@test.com"), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        harness.Repository.Verify(r => r.CreateBatchRunAsync(It.IsAny<BatchRun>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteBatchAsync_StopsWhenDailyCapAlreadyReached()
    {
        var harness = CreateHarness(TwoJobs(), new AppSettings { DelayBetweenSendsMs = 0, DailySendCap = 1 });
        harness.Repository.Setup(r => r.CountSentSinceAsync(It.IsAny<DateTime>())).ReturnsAsync(1);

        var request = new BatchRequest { ExcelFilePath = "dummy.xlsx", Template = harness.Template };
        var summary = await harness.Service.ExecuteBatchAsync(request);

        Assert.Equal(BatchRunStatus.DailyCapReached, summary.Status);
        harness.EmailSender.Verify(x => x.SendEmailAsync(It.IsAny<EmailJob>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
