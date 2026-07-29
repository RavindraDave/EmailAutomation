using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Google;
using EmailAutomation.Infrastructure.Email;
using Xunit;

namespace EmailAutomation.Tests;

public class SmtpEmailSenderRetryTests
{
    private static SmtpEmailSender CreateSender() => new("smtp.example.com", 587, "user@example.com", "password");

    [Fact]
    public void IsTransientError_ReturnsFalse_ForAuthenticationFailure()
    {
        // Regression guard: retrying a bad-password failure just wastes time and can trip
        // provider account lockouts - it must not be classified as transient.
        var sender = CreateSender();
        var ex = new MailKit.Security.AuthenticationException("bad credentials");

        Assert.False(sender.IsTransientError(ex));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TimeoutException))]
    public void IsTransientError_ReturnsTrue_ForNetworkFailures(Type exceptionType)
    {
        var sender = CreateSender();
        var ex = (Exception)Activator.CreateInstance(exceptionType, "network blip")!;

        Assert.True(sender.IsTransientError(ex));
    }
}

public class GmailSenderRetryTests
{
    private static GmailSender CreateSender() => new("credentials.json", "token.json");

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void IsTransientError_ReturnsFalse_ForAuthOrPermissionFailures(HttpStatusCode statusCode)
    {
        // Regression guard for the original bug where IsTransientError always returned true,
        // meaning a revoked/invalid credential would be retried pointlessly on every send.
        var sender = CreateSender();
        var ex = new GoogleApiException("gmail", "denied") { HttpStatusCode = statusCode };

        Assert.False(sender.IsTransientError(ex));
    }

    [Theory]
    [InlineData((HttpStatusCode)429)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void IsTransientError_ReturnsTrue_ForRateLimitOrServerErrors(HttpStatusCode statusCode)
    {
        var sender = CreateSender();
        var ex = new GoogleApiException("gmail", "try again") { HttpStatusCode = statusCode };

        Assert.True(sender.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_ReturnsFalse_ForOtherClientErrors()
    {
        var sender = CreateSender();
        var ex = new GoogleApiException("gmail", "bad request") { HttpStatusCode = HttpStatusCode.BadRequest };

        Assert.False(sender.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_ReturnsTrue_ForNetworkLevelFailures()
    {
        var sender = CreateSender();
        var ex = new HttpRequestException("connection reset");

        Assert.True(sender.IsTransientError(ex));
    }
}
