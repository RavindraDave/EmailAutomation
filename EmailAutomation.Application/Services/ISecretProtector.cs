namespace EmailAutomation.Application.Services;

/// <summary>
/// Protects a secret (e.g. an SMTP password) using the current OS user's credential store
/// (Windows DPAPI, macOS Keychain). Never persist plaintext secrets to disk - always round-trip
/// through this interface first.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts/stores plaintext and returns an opaque payload safe to persist in a settings file.</summary>
    string Protect(string plaintext);

    /// <summary>Reverses Protect. Returns null if the payload cannot be decrypted (e.g. moved to another machine/user).</summary>
    string? Unprotect(string protectedPayload);
}
