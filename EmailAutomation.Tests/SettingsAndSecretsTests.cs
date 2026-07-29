using System;
using System.IO;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Security;
using EmailAutomation.Infrastructure.Settings;
using Moq;
using Xunit;

namespace EmailAutomation.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void CurrentPlatformProtector_RoundTripsSecret()
    {
        var protector = SecretProtectorFactory.Create();
        const string secret = "correct horse battery staple";

        var protectedPayload = protector.Protect(secret);
        var recovered = protector.Unprotect(protectedPayload);

        Assert.Equal(secret, recovered);
        Assert.NotEqual(secret, protectedPayload); // must not just echo the plaintext back
    }

    [Fact]
    public void Unprotect_ReturnsNull_ForGarbagePayload()
    {
        var protector = SecretProtectorFactory.Create();

        // A payload this protector never produced (wrong marker/format/corrupted) must fail closed,
        // not throw and not return garbage that looks like a password.
        var result = protector.Unprotect("not-a-real-payload-" + Guid.NewGuid());

        Assert.Null(result);
    }
}

public class JsonSettingsServiceTests
{
    private static Mock<ISecretProtector> CreateFakeProtector()
    {
        // Base64 round-trip stands in for real encryption here: it's reversible (so GetPassword
        // still works) but the plaintext no longer appears verbatim, which is what the
        // "never persisted as plaintext" assertions below actually need to exercise.
        var mock = new Mock<ISecretProtector>();
        mock.Setup(p => p.Protect(It.IsAny<string>()))
            .Returns<string>(s => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)));
        mock.Setup(p => p.Unprotect(It.IsAny<string>()))
            .Returns<string>(s =>
            {
                try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
                catch (FormatException) { return null; }
            });
        return mock;
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSettingsAndPassword()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}.json");
        try
        {
            var protector = CreateFakeProtector();
            var service = new JsonSettingsService(tempPath, protector.Object);

            var settings = new AppSettings
            {
                Provider = "SMTP",
                SmtpHost = "smtp.example.com",
                SmtpPort = 465,
                Username = "user@example.com",
                DelayBetweenSendsMs = 2000,
                DailySendCap = 300,
            };
            service.SetPassword(settings, "hunter2");

            await service.SaveAsync(settings);
            var loaded = await service.LoadAsync();

            Assert.Equal("smtp.example.com", loaded.SmtpHost);
            Assert.Equal(465, loaded.SmtpPort);
            Assert.Equal("user@example.com", loaded.Username);
            Assert.Equal(2000, loaded.DelayBetweenSendsMs);
            Assert.Equal(300, loaded.DailySendCap);
            Assert.Equal("hunter2", service.GetPassword(loaded));
            Assert.DoesNotContain("hunter2", loaded.EncryptedPassword ?? string.Empty);
            Assert.DoesNotContain("hunter2", await File.ReadAllTextAsync(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"settings_missing_{Guid.NewGuid():N}.json");
        var service = new JsonSettingsService(tempPath, CreateFakeProtector().Object);

        var settings = await service.LoadAsync();

        Assert.Equal("SMTP", settings.Provider);
        Assert.False(settings.HasCredentials);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileIsCorrupted()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"settings_corrupt_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath, "{ this is not valid json");
            var service = new JsonSettingsService(tempPath, CreateFakeProtector().Object);

            var settings = await service.LoadAsync();

            Assert.Equal("SMTP", settings.Provider);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
