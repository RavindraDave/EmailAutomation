using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using EmailAutomation.Application.Services;

namespace EmailAutomation.Infrastructure.Security;

/// <summary>
/// Windows-only secret protector backed by DPAPI, scoped to the current OS user. The ciphertext
/// is safe to persist in settings.json - it cannot be decrypted by another user or another machine.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsDpapiSecretProtector : ISecretProtector
{
    // Ties ciphertext to this app so another app's DPAPI blobs can't be swapped in.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EmailAutomation.v1");

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    public string? Unprotect(string protectedPayload)
    {
        try
        {
            var cipherBytes = Convert.FromBase64String(protectedPayload);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception)
        {
            // Wrong user, wrong machine, or corrupted payload - caller should treat this as "no password set".
            return null;
        }
    }
}
