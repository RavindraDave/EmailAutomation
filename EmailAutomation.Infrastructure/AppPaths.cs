using System;
using System.IO;

namespace EmailAutomation.Infrastructure;

/// <summary>
/// Single source of truth for where this app stores per-user data (database, settings, logs).
/// Never resolve these paths relative to the current working directory - a double-clicked
/// .app bundle or Start Menu shortcut does not guarantee any particular cwd.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "EmailAutomation";

    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string DatabasePath => Path.Combine(DataDirectory, "email_automation.db");

    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    private static string ResolveDataDirectory()
    {
        string baseDir;

        if (OperatingSystem.IsMacOS())
        {
            // Environment.SpecialFolder.ApplicationData maps to ~/.config on Unix via .NET's
            // Unix mapping, which is wrong for a macOS GUI app - use the real Application Support path.
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, "Library", "Application Support");
        }
        else if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            // Linux and anything else: honor XDG_DATA_HOME if set, else ~/.local/share
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrEmpty(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        var dataDirectory = Path.Combine(baseDir, AppFolderName);
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(Path.Combine(dataDirectory, "logs"));
        return dataDirectory;
    }
}
