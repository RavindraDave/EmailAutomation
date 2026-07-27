using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Services;

public interface IRepository
{
    Task<IEnumerable<EmailTemplate>> GetTemplatesAsync();
    Task<EmailTemplate?> GetTemplateByIdAsync(Guid id);
    Task AddTemplateAsync(EmailTemplate template);
    Task UpdateTemplateAsync(EmailTemplate template);
    Task DeleteTemplateAsync(Guid id);

    Task<Guid> CreateBatchRunAsync(BatchRun run);
    Task CompleteBatchRunAsync(Guid batchId, string status, DateTime completedAt, int sentCount, int failedCount, int skippedCount);
    Task<BatchRun?> FindResumableRunAsync(string excelFilePath, Guid templateId);
    Task<IReadOnlySet<int>> GetSentRowNumbersAsync(Guid batchId);
    Task UpsertEmailLogAsync(EmailLog log);
    Task<int> CountSentSinceAsync(DateTime sinceUtc);
    Task<IEnumerable<EmailLog>> GetLogsForBatchAsync(Guid batchId);
    Task<IEnumerable<BatchRun>> GetRecentRunsAsync(int take);
    Task<(int TotalSuccess, int TotalFailure)> GetOverallEmailCountsAsync();
}
