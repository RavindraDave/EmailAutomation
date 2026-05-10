namespace EmailAutomation.Domain.Models;

public class SendResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; }
    public string? GmailMessageId { get; set; }
}
