using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using Google;
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
    private GmailService? _service;

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
        // The OAuth flow (including any interactive browser consent) only needs to run once per
        // process - reuse the authenticated service instead of re-authorizing on every send.
        if (_service != null)
        {
            return _service;
        }

        using var stream = new FileStream(_credentialsPath, FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(_tokenPath, true));

        _service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "EmailAutomation",
        });

        return _service;
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

    // internal (not private) + InternalsVisibleTo so this retry-classification logic can be unit
    // tested directly - GmailSender otherwise makes real OAuth/HTTP calls with no seam to mock.
    internal bool IsTransientError(Exception ex)
    {
        if (ex is GoogleApiException apiEx)
        {
            var statusCode = apiEx.HttpStatusCode;

            // Bad/expired/revoked credentials or insufficient permission - retrying won't help.
            if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
            {
                return false;
            }

            // Rate-limited or server-side failures are worth retrying.
            if (statusCode == (HttpStatusCode)429 || (int)statusCode >= 500)
            {
                return true;
            }

            // Other 4xx responses (bad request, not found, etc.) are permanent failures.
            return false;
        }

        // Network-level failures are worth a retry.
        return ex is HttpRequestException or IOException or TimeoutException or TaskCanceledException;
    }
}
