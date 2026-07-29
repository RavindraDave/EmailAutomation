using System;
using System.IO;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Polly;
using Polly.Retry;

namespace EmailAutomation.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly AsyncRetryPolicy _retryPolicy;

    public SmtpEmailSender(string host, int port, string username, string password)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;

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

                var message = CreateMimeMessage(job, renderedSubject, renderedBody);

                using var client = new SmtpClient();
                await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_username, _password);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                // SMTP doesn't have a direct "GmailMessageId", using a generic ID or empty.
                result.GmailMessageId = message.MessageId;
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

    private MimeMessage CreateMimeMessage(EmailJob job, string subject, string bodyHtml)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_username, _username));
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

    // internal (not private) + InternalsVisibleTo so this retry-classification logic is unit tested directly.
    internal bool IsTransientError(Exception ex)
    {
        if (ex is AuthenticationException) return false;

        return true;
    }
}
