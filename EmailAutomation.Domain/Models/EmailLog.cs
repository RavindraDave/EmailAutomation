using System;

namespace EmailAutomation.Domain.Models;

/// <summary>Per-row outcome of a batch run. (BatchId, RowNumber) is unique so re-processing a row
/// (e.g. on resume) updates the existing record instead of creating a duplicate.</summary>
public class EmailLog
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Status { get; set; } = EmailLogStatus.Failed;
    public int Attempts { get; set; }
    public string? ErrorMessage { get; set; }
    public string? GmailMessageId { get; set; }
    public DateTime? SentAt { get; set; }
}

public static class EmailLogStatus
{
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}
