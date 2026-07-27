using System;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Services;

/// <summary>Input to a batch run. Set ResumeBatchId to continue a previously interrupted run
/// instead of starting a new one - rows already marked Sent in that batch are skipped.</summary>
public class BatchRequest
{
    public required string ExcelFilePath { get; init; }
    public required EmailTemplate Template { get; init; }
    public Guid? ResumeBatchId { get; init; }
}

/// <summary>Snapshot reported after every row, immutable so consumers can safely hold onto
/// values reported via IProgress&lt;BatchProgress&gt; without them changing underneath.</summary>
public sealed record BatchProgress(
    int Total,
    int Processed,
    int Succeeded,
    int Failed,
    int Skipped,
    string? CurrentRecipient);

/// <summary>Final outcome of ExecuteBatchAsync.</summary>
public sealed record BatchSummary(
    Guid BatchId,
    string Status,
    int TotalRows,
    int SentCount,
    int FailedCount,
    int SkippedCount);
