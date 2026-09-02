using EmailAutomation.Infrastructure.Email;
using Xunit;

namespace EmailAutomation.Tests;

public class MimeKitEmailAddressValidatorTests
{
    private readonly MimeKitEmailAddressValidator _validator = new();

    [Theory]
    [InlineData("a@x.com;b@x.com;c@x.com")]
    [InlineData("a@x.com; b@x.com; c@x.com")]
    [InlineData("a@x.com,b@x.com")]
    [InlineData("a@x.com")]
    public void IsValid_AcceptsSemicolonOrCommaSeparatedLists(string addresses)
    {
        Assert.True(_validator.IsValid(addresses));
    }

    [Theory]
    [InlineData("a@x.com;not-an-email")]
    [InlineData(";;")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_RejectsListsWithAnyInvalidOrEmptyEntry(string? addresses)
    {
        Assert.False(_validator.IsValid(addresses));
    }
}
