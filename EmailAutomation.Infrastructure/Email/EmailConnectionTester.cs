using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace EmailAutomation.Infrastructure.Email;

public class EmailConnectionTester : IEmailConnectionTester
{
    public async Task<(bool Success, string Message)> TestAsync(AppSettings settings, string plaintextPassword)
    {
        try
        {
            if (settings.Provider.Equals("GmailAPI", StringComparison.OrdinalIgnoreCase))
            {
                return await TestGmailApiAsync(settings);
            }

            return await TestSmtpAsync(settings, plaintextPassword);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task<(bool, string)> TestSmtpAsync(AppSettings settings, string plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword))
        {
            return (false, "No password entered. Type your app password before testing.");
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(settings.Username, plaintextPassword);
        await client.DisconnectAsync(true);
        return (true, $"Connected and authenticated as {settings.Username}.");
    }

    private static async Task<(bool, string)> TestGmailApiAsync(AppSettings settings)
    {
        if (!File.Exists(settings.CredentialsPath))
        {
            return (false, $"Credentials file not found: {settings.CredentialsPath}");
        }

        using var stream = new FileStream(settings.CredentialsPath, FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { GmailService.Scope.GmailSend },
            "user",
            CancellationToken.None,
            new FileDataStore(settings.TokenPath, true));

        using var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "EmailAutomation",
        });

        var profile = await service.Users.GetProfile("me").ExecuteAsync();
        return (true, $"Connected as {profile.EmailAddress}.");
    }
}
