using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using EmailAutomation.Application.Services;

namespace EmailAutomation.Infrastructure.Security;

/// <summary>
/// macOS-only secret protector backed by the login Keychain via the `security` CLI. This avoids
/// native P/Invoke bindings to Keychain Services, at the cost of a brief window where the
/// plaintext password is visible as a process argument to other processes on the same machine
/// (e.g. via `ps`) while `security add-generic-password` runs. That's an accepted trade-off for
/// this desktop MVP; a native Keychain Services binding would close that gap if ever needed.
///
/// The "ciphertext" this class hands back is just a marker - the real secret lives in Keychain,
/// keyed by a fixed service/account pair, since this app manages exactly one SMTP identity at a time.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOsKeychainSecretProtector : ISecretProtector
{
    private const string ServiceName = "EmailAutomation";
    private const string AccountName = "smtp-credential";
    private const string KeychainMarker = "keychain-ref:v1";

    public string Protect(string plaintext)
    {
        // Clear any prior entry first - add-generic-password fails with "already exists" otherwise,
        // and -U (update) alone is not reliably atomic across all macOS versions.
        RunSecurity(new[] { "delete-generic-password", "-a", AccountName, "-s", ServiceName });

        var (exitCode, _, stderr) = RunSecurity(
            new[] { "add-generic-password", "-a", AccountName, "-s", ServiceName, "-w", plaintext, "-U" });

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to store credential in macOS Keychain: {stderr}");
        }

        return KeychainMarker;
    }

    public string? Unprotect(string protectedPayload)
    {
        if (protectedPayload != KeychainMarker)
        {
            return null;
        }

        var (exitCode, stdout, _) = RunSecurity(
            new[] { "find-generic-password", "-a", AccountName, "-s", ServiceName, "-w" });

        return exitCode == 0 ? stdout.TrimEnd('\r', '\n') : null;
    }

    private static (int ExitCode, string StdOut, string StdErr) RunSecurity(string[] args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("/usr/bin/security")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
