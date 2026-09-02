using System.Linq;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using MimeKit;

namespace EmailAutomation.Infrastructure.Email;

public class MimeKitEmailAddressValidator : IEmailAddressValidator
{
    public bool IsValid(string? address)
    {
        // address may hold one or several mailboxes separated by ';' or ',' (e.g. a Cc list).
        var parts = EmailAddressList.Split(address);
        return parts.Count > 0 && parts.All(IsValidSingle);
    }

    private static bool IsValidSingle(string address)
    {
        // MimeKit's parser is deliberately lenient (it accepts a bare local-part with no "@" as
        // a technically-valid mailbox), which would let obvious typos like "not-an-email" through.
        // Requiring an "@" catches that case while still relying on MimeKit for real RFC parsing.
        return address.Contains('@') && MailboxAddress.TryParse(address, out _);
    }
}
