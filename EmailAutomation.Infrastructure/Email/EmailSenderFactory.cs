using System;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Infrastructure.Email;

public class EmailSenderFactory : IEmailSenderFactory
{
    private readonly ISecretProtector _protector;

    public EmailSenderFactory(ISecretProtector protector)
    {
        _protector = protector;
    }

    public IEmailSender Create(AppSettings settings)
    {
        if (settings.Provider.Equals("GmailAPI", StringComparison.OrdinalIgnoreCase))
        {
            return new GmailSender(settings.CredentialsPath, settings.TokenPath);
        }

        var password = string.IsNullOrEmpty(settings.EncryptedPassword)
            ? string.Empty
            : _protector.Unprotect(settings.EncryptedPassword) ?? string.Empty;

        return new SmtpEmailSender(settings.SmtpHost, settings.SmtpPort, settings.Username, password);
    }
}
