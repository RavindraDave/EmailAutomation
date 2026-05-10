using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MimeKit;
using Polly;
using Polly.Retry;

namespace EmailAutomation.Infrastructure.Email;

public class GmailSender : IEmailSender
{
    private static readonly string[] Scopes = { GmailService.Scope.GmailSend };
    private readonly string _credentialsPath;
    private readonly string _tokenPath;
    private readonly AsyncRetryPolicy _retryPolicy;

    public GmailSender(string credentialsPath, string tokenPath)
    {
        _credentialsPath = credentialsPath;
        _tokenPath = tokenPath;

        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => attempt == 1 ? TimeSpan.Zero : TimeSpan.FromSeconds(attempt == 2 ? 2 : 5)
            );
    }

    public async Task<SendResult> SendEmailAsync(EmailJob job, string renderedSubject, string renderedBody)
    {
        var result = new SendResult();
        int attempts = 0;

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                attempts++;
                var service = await GetGmailServiceAsync();
                var message = CreateMimeMessage(job, renderedSubject, renderedBody);
                var gmailMessage = new Message { Raw = Base64UrlEncode(message.ToString()) };

                var request = service.Users.Messages.Send(gmailMessage, "me");
                var response = await request.ExecuteAsync();

                result.GmailMessageId = response.Id;
                result.Success = true;
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.Attempts = attempts;
        }

        return result;
    }

    private async Task<GmailService> GetGmailServiceAsync()
    {
        using var stream = new FileStream(_credentialsPath, FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(_tokenPath, true));

        return new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "EmailAutomation",
        });
    }

    private MimeMessage CreateMimeMessage(EmailJob job, string subject, string bodyHtml)
    {
        var message = new MimeMessage();
        message.To.Add(MailboxAddress.Parse(job.To));

        if (!string.IsNullOrWhiteSpace(job.Cc))
        {
            message.Cc.Add(MailboxAddress.Parse(job.Cc));
        }

        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = bodyHtml
        };

        if (!string.IsNullOrWhiteSpace(job.AttachmentPath) && File.Exists(job.AttachmentPath))
        {
            builder.Attachments.Add(job.AttachmentPath);
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    private string Base64UrlEncode(string input)
    {
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(inputBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    private bool IsTransientError(Exception ex)
    {
        return true;
    }
}
