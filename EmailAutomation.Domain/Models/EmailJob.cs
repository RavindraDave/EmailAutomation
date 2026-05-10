using System.Collections.Generic;

namespace EmailAutomation.Domain.Models;

public class EmailJob
{
    public int RowNumber { get; set; }
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Subject { get; set; }
    public string? AttachmentPath { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}
