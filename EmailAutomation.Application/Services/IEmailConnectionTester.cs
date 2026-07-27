using System.Threading.Tasks;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Services;

public interface IEmailConnectionTester
{
    /// <summary>
    /// Attempts to connect and authenticate against the configured provider without sending
    /// anything. Used by the Settings screen's "Test Connection" button.
    /// </summary>
    Task<(bool Success, string Message)> TestAsync(AppSettings settings, string plaintextPassword);
}
