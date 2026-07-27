using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Services;

/// <summary>
/// Builds an IEmailSender from the current AppSettings. Introduced so the sender can be
/// reconfigured at runtime after the user edits Settings, instead of being fixed at
/// DI-container-build time.
/// </summary>
public interface IEmailSenderFactory
{
    IEmailSender Create(AppSettings settings);
}
