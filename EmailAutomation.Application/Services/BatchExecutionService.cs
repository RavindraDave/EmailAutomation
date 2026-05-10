using System;
using System.Collections.Generic;
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
    private readonly IEmailSender _emailSender;

    public BatchExecutionService(
        IExcelReader excelReader,
        ITemplateEngine templateEngine,
        IEmailSender emailSender)
    {
        _excelReader = excelReader;
        _templateEngine = templateEngine;
        _emailSender = emailSender;
    }

    public async Task ExecuteBatchAsync(string excelFilePath, EmailTemplate template, CancellationToken cancellationToken = default)
    {
        Log.Information("Starting batch execution from {ExcelFilePath} with template {TemplateId}", excelFilePath, template.Id);

        IEnumerable<EmailJob> jobs;
        try
        {
            jobs = _excelReader.ReadJobs(excelFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read excel file: {ExcelFilePath}", excelFilePath);
            return;
        }

        foreach (var job in jobs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Log.Information("Batch execution cancelled.");
                break;
            }

            try
            {
                var subjectToRender = job.Subject ?? template.SubjectTemplate;
                var renderedSubject = _templateEngine.Render(subjectToRender, job.Variables);
                var renderedBody = _templateEngine.Render(template.BodyTemplate, job.Variables);

                if (!string.IsNullOrEmpty(job.AttachmentPath))
                {
                    job.AttachmentPath = _templateEngine.Render(job.AttachmentPath, job.Variables);
                }

                var result = await _emailSender.SendEmailAsync(job, renderedSubject, renderedBody);

                if (result.Success)
                {
                    Log.Information("Row {RowNumber}: Sent email successfully to {To}. MessageId: {GmailMessageId}. Attempts: {Attempts}", job.RowNumber, job.To, result.GmailMessageId, result.Attempts);
                }
                else
                {
                    Log.Error("Row {RowNumber}: Failed to send email to {To}. Error: {ErrorMessage}. Attempts: {Attempts}", job.RowNumber, job.To, result.ErrorMessage, result.Attempts);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Row {RowNumber}: Unexpected error processing job for {To}", job.RowNumber, job.To);
            }
        }

        Log.Information("Completed batch execution for {ExcelFilePath}", excelFilePath);
    }
}
