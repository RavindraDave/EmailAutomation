namespace EmailAutomation.Application.Services;

public interface IEmailAddressValidator
{
    bool IsValid(string? address);
}
