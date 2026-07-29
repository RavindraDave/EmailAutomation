using EmailAutomation.Application.Services;
using MimeKit;

namespace EmailAutomation.Infrastructure.Email;

public class MimeKitEmailAddressValidator : IEmailAddressValidator
{
    public bool IsValid(string? address)
    {
        // MimeKit's parser is deliberately lenient (it accepts a bare local-part with no "@" as
        // a technically-valid mailbox), which would let obvious typos like "not-an-email" through.
        // Requiring an "@" catches that case while still relying on MimeKit for real RFC parsing.
        return !string.IsNullOrWhiteSpace(address)
            && address.Contains('@')
            && MailboxAddress.TryParse(address, out _);
    }
}
