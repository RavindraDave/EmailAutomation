using System.Threading.Tasks;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Services;

public interface ISettingsService
{
    /// <summary>Loads persisted settings, or defaults if none exist yet (first run).</summary>
    Task<AppSettings> LoadAsync();

    /// <summary>Persists settings. The Password on the passed-in settings is expected to already be encrypted.</summary>
    Task SaveAsync(AppSettings settings);

    /// <summary>Convenience: encrypts plaintext via ISecretProtector and stores it on the settings object.</summary>
    void SetPassword(AppSettings settings, string plaintextPassword);

    /// <summary>Convenience: decrypts the settings object's stored password for use by a sender.</summary>
    string? GetPassword(AppSettings settings);
}
