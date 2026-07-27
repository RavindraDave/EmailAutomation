namespace EmailAutomation.Domain.Models;

/// <summary>
/// User-configurable application settings, persisted per-user outside the install directory.
/// EncryptedPassword is always ciphertext (or a platform keychain reference) - the plaintext
/// password only ever exists in memory for the duration of a Test Connection or a send.
/// </summary>
public class AppSettings
{
    public string Provider { get; set; } = "SMTP"; // "SMTP" or "GmailAPI"

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;

    /// <summary>Ciphertext produced by ISecretProtector.Protect - never store plaintext here.</summary>
    public string? EncryptedPassword { get; set; }

    public string CredentialsPath { get; set; } = "credentials.json";
    public string TokenPath { get; set; } = "token.json";

    /// <summary>Minimum delay between sends, to stay under provider rate limits.</summary>
    public int DelayBetweenSendsMs { get; set; } = 1000;

    /// <summary>Safety cap on sends per rolling 24h window (Gmail consumer accounts are limited to ~500/day).</summary>
    public int DailySendCap { get; set; } = 450;

    public bool HasCredentials => Provider.Equals("GmailAPI", System.StringComparison.OrdinalIgnoreCase)
        ? true // Gmail API credentials are validated via the credentials/token files, not a password
        : !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(EncryptedPassword);
}
