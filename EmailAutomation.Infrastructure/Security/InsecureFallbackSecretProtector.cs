using System;
using System.Text;
using EmailAutomation.Application.Services;
using Serilog;

namespace EmailAutomation.Infrastructure.Security;

/// <summary>
/// Last-resort protector for platforms with neither DPAPI nor Keychain (e.g. Linux dev builds).
/// This is Base64 obfuscation only, NOT encryption - it exists so the app doesn't crash outside
/// its two shipped platforms (Windows, macOS), not to protect a real secret. Every use is logged
/// as a warning.
/// </summary>
public class InsecureFallbackSecretProtector : ISecretProtector
{
    public string Protect(string plaintext)
    {
        Log.Warning("Storing credential using the insecure fallback protector - this platform has no DPAPI/Keychain support. Do not use real credentials in production on this OS.");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    }

    public string? Unprotect(string protectedPayload)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedPayload));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
