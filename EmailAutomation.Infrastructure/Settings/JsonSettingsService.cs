using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using Serilog;

namespace EmailAutomation.Infrastructure.Settings;

public class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ISecretProtector _protector;

    public JsonSettingsService(ISecretProtector protector)
        : this(AppPaths.SettingsPath, protector)
    {
    }

    public JsonSettingsService(string path, ISecretProtector protector)
    {
        _path = path;
        _protector = protector;
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings from {Path}; falling back to defaults", _path);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var stream = File.Create(_path))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not restrict permissions on {Path}", _path);
            }
        }
    }

    public void SetPassword(AppSettings settings, string plaintextPassword)
    {
        settings.EncryptedPassword = string.IsNullOrEmpty(plaintextPassword)
            ? null
            : _protector.Protect(plaintextPassword);
    }

    public string? GetPassword(AppSettings settings)
    {
        return string.IsNullOrEmpty(settings.EncryptedPassword)
            ? null
            : _protector.Unprotect(settings.EncryptedPassword);
    }
}
