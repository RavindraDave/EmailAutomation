using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmailAutomation.Domain.Models;
using Serilog;

namespace EmailAutomation.Application.Services;

public interface ITemplateEngine
{
    string Render(string template, Dictionary<string, string> variables);
}

public interface IExcelReader
{
    IEnumerable<EmailJob> ReadJobs(string filePath);
}

public interface IEmailSender
{
    Task<SendResult> SendEmailAsync(EmailJob job, string renderedSubject, string renderedBody);
}

public class BatchExecutionService
{
    private readonly IExcelReader _excelReader;
    private readonly ITemplateEngine _templateEngine;
    private readonly IEmailSenderFactory _emailSenderFactory;
    private readonly ISettingsService _settingsService;
    private readonly IRepository _repository;
    private readonly TimeProvider _timeProvider;

    public BatchExecutionService(
        IExcelReader excelReader,
        ITemplateEngine templateEngine,
        IEmailSenderFactory emailSenderFactory,
        ISettingsService settingsService,
        IRepository repository,
        TimeProvider? timeProvider = null)
    {
        _excelReader = excelReader;
        _templateEngine = templateEngine;
        _emailSenderFactory = emailSenderFactory;
        _settingsService = settingsService;
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BatchSummary> ExecuteBatchAsync(
        BatchRequest request,
        IProgress<BatchProgress>? progress = null,
        PauseToken pauseToken = default,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Starting batch execution from {ExcelFilePath} with template {TemplateId}", request.ExcelFilePath, request.Template.Id);

        List<EmailJob> jobs;
        try
        {
            jobs = _excelReader.ReadJobs(request.ExcelFilePath).ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read excel file: {ExcelFilePath}", request.ExcelFilePath);
            throw;
        }

        var settings = await _settingsService.LoadAsync();
        var emailSender = _emailSenderFactory.Create(settings);

        Guid batchId;
        IReadOnlySet<int> alreadySentRows;

        if (request.ResumeBatchId is { } resumeId)
        {
            batchId = resumeId;
            alreadySentRows = await _repository.GetSentRowNumbersAsync(batchId);
            Log.Information("Resuming batch {BatchId}; {Count} row(s) already sent will be skipped.", batchId, alreadySentRows.Count);
        }
        else
        {
            batchId = await _repository.CreateBatchRunAsync(new BatchRun
            {
                ExcelFilePath = request.ExcelFilePath,
                TemplateId = request.Template.Id,
                StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
                Status = BatchRunStatus.Running,
                TotalRows = jobs.Count,
            });
            alreadySentRows = new HashSet<int>();
        }

        var total = jobs.Count;
        var processed = 0;
        var succeededCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var finalStatus = BatchRunStatus.Completed;

        void ReportProgress(string? currentRecipient) =>
            progress?.Report(new BatchProgress(total, processed, succeededCount, failedCount, skippedCount, currentRecipient));

        try
        {
            foreach (var job in jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await pauseToken.WaitWhilePausedAsync(cancellationToken);

                if (alreadySentRows.Contains(job.RowNumber))
                {
                    skippedCount++;
                    processed++;
                    ReportProgress(job.To);
                    continue;
                }

                var todayUtc = _timeProvider.GetUtcNow().UtcDateTime.Date;
                var sentToday = await _repository.CountSentSinceAsync(todayUtc);
                if (sentToday >= settings.DailySendCap)
                {
                    finalStatus = BatchRunStatus.DailyCapReached;
                    Log.Warning("Daily send cap of {Cap} reached; stopping batch {BatchId}.", settings.DailySendCap, batchId);
                    break;
                }

                var logEntry = await ProcessRowAsync(job, request.Template, batchId, emailSender);
                await _repository.UpsertEmailLogAsync(logEntry);

                if (logEntry.Status == EmailLogStatus.Sent)
                {
                    succeededCount++;
                }
                else
                {
                    failedCount++;
                }

                processed++;
                ReportProgress(job.To);

                if (settings.DelayBetweenSendsMs > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(settings.DelayBetweenSendsMs), _timeProvider, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            finalStatus = BatchRunStatus.Cancelled;
            Log.Information("Batch {BatchId} cancelled.", batchId);
        }

        // Source of truth for final tallies is EmailLogs, not the in-memory counters above -
        // on a resumed run, rows sent in a *previous* invocation must still count toward the total.
        var allLogs = (await _repository.GetLogsForBatchAsync(batchId)).ToList();
        var totalSent = allLogs.Count(l => l.Status == EmailLogStatus.Sent);
        var totalFailed = allLogs.Count(l => l.Status == EmailLogStatus.Failed);

        await _repository.CompleteBatchRunAsync(batchId, finalStatus, _timeProvider.GetUtcNow().UtcDateTime, totalSent, totalFailed, skippedCount);

        Log.Information(
            "Completed batch execution for {ExcelFilePath} with status {Status} ({Sent} sent, {Failed} failed, {Skipped} skipped)",
            request.ExcelFilePath, finalStatus, totalSent, totalFailed, skippedCount);

        return new BatchSummary(batchId, finalStatus, jobs.Count, totalSent, totalFailed, skippedCount);
    }

    private async Task<EmailLog> ProcessRowAsync(EmailJob job, EmailTemplate template, Guid batchId, IEmailSender emailSender)
    {
        try
        {
            var subjectToRender = job.Subject ?? template.SubjectTemplate;
            var renderedSubject = _templateEngine.Render(subjectToRender, job.Variables);
            var renderedBody = _templateEngine.Render(template.BodyTemplate, job.Variables);

            var jobToSend = job;
            if (!string.IsNullOrEmpty(job.AttachmentPath))
            {
                var renderedAttachmentPath = _templateEngine.Render(job.AttachmentPath, job.Variables);

                // Render into a copy rather than mutating the shared job - a retried or resumed
                // row must render the original template again, not an already-rendered path.
                jobToSend = new EmailJob
                {
                    RowNumber = job.RowNumber,
                    To = job.To,
                    Cc = job.Cc,
                    Subject = job.Subject,
                    AttachmentPath = renderedAttachmentPath,
                    Variables = job.Variables,
                };
            }

            var result = await emailSender.SendEmailAsync(jobToSend, renderedSubject, renderedBody);

            if (result.Success)
            {
                Log.Information("Row {RowNumber}: Sent email successfully to {To}. MessageId: {GmailMessageId}. Attempts: {Attempts}", job.RowNumber, job.To, result.GmailMessageId, result.Attempts);
                return new EmailLog
                {
                    BatchId = batchId,
                    RowNumber = job.RowNumber,
                    Recipient = job.To,
                    Subject = renderedSubject,
                    Status = EmailLogStatus.Sent,
                    Attempts = result.Attempts,
                    GmailMessageId = result.GmailMessageId,
                    SentAt = _timeProvider.GetUtcNow().UtcDateTime,
                };
            }

            Log.Error("Row {RowNumber}: Failed to send email to {To}. Error: {ErrorMessage}. Attempts: {Attempts}", job.RowNumber, job.To, result.ErrorMessage, result.Attempts);
            return new EmailLog
            {
                BatchId = batchId,
                RowNumber = job.RowNumber,
                Recipient = job.To,
                Subject = renderedSubject,
                Status = EmailLogStatus.Failed,
                Attempts = result.Attempts,
                ErrorMessage = result.ErrorMessage,
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Row {RowNumber}: Unexpected error processing job for {To}", job.RowNumber, job.To);
            return new EmailLog
            {
                BatchId = batchId,
                RowNumber = job.RowNumber,
                Recipient = job.To,
                Status = EmailLogStatus.Failed,
                Attempts = 0,
                ErrorMessage = ex.Message,
            };
        }
    }
}
