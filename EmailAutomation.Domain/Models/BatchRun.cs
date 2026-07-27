using System;

namespace EmailAutomation.Domain.Models;

/// <summary>One execution of a template against an Excel file - tracked so runs can be resumed and reported on.</summary>
public class BatchRun
{
    public Guid Id { get; set; }
    public string ExcelFilePath { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = BatchRunStatus.Running;
    public int TotalRows { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
}

public static class BatchRunStatus
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
    public const string DailyCapReached = "DailyCapReached";
}
