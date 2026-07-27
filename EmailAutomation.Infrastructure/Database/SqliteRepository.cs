using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Infrastructure.Database;

public class SqliteRepository : IRepository
{
    private readonly string _connectionString;

    static SqliteRepository()
    {
        GuidTypeHandler.Register();
    }

    public SqliteRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<EmailTemplate>> GetTemplatesAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<EmailTemplate>("SELECT * FROM EmailTemplates");
    }

    public async Task<EmailTemplate?> GetTemplateByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<EmailTemplate>(
            "SELECT * FROM EmailTemplates WHERE Id = @Id", new { Id = id.ToString() });
    }

    public async Task AddTemplateAsync(EmailTemplate template)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "INSERT INTO EmailTemplates (Id, Name, SubjectTemplate, BodyTemplate) VALUES (@Id, @Name, @SubjectTemplate, @BodyTemplate)",
            new { Id = template.Id.ToString(), template.Name, template.SubjectTemplate, template.BodyTemplate });
    }

    public async Task UpdateTemplateAsync(EmailTemplate template)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE EmailTemplates SET Name = @Name, SubjectTemplate = @SubjectTemplate, BodyTemplate = @BodyTemplate WHERE Id = @Id",
            new { Id = template.Id.ToString(), template.Name, template.SubjectTemplate, template.BodyTemplate });
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM EmailTemplates WHERE Id = @Id", new { Id = id.ToString() });
    }

    public async Task<Guid> CreateBatchRunAsync(BatchRun run)
    {
        if (run.Id == Guid.Empty)
        {
            run.Id = Guid.NewGuid();
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO BatchRuns (Id, ExcelFilePath, TemplateId, StartedAt, CompletedAt, Status, TotalRows, SentCount, FailedCount, SkippedCount)
            VALUES (@Id, @ExcelFilePath, @TemplateId, @StartedAt, @CompletedAt, @Status, @TotalRows, @SentCount, @FailedCount, @SkippedCount)",
            new
            {
                Id = run.Id.ToString(),
                run.ExcelFilePath,
                TemplateId = run.TemplateId.ToString(),
                run.StartedAt,
                run.CompletedAt,
                run.Status,
                run.TotalRows,
                run.SentCount,
                run.FailedCount,
                run.SkippedCount,
            });

        return run.Id;
    }

    public async Task CompleteBatchRunAsync(Guid batchId, string status, DateTime completedAt, int sentCount, int failedCount, int skippedCount)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            UPDATE BatchRuns
            SET Status = @Status, CompletedAt = @CompletedAt, SentCount = @SentCount, FailedCount = @FailedCount, SkippedCount = @SkippedCount
            WHERE Id = @Id",
            new
            {
                Id = batchId.ToString(),
                Status = status,
                CompletedAt = completedAt,
                SentCount = sentCount,
                FailedCount = failedCount,
                SkippedCount = skippedCount,
            });
    }

    public async Task<BatchRun?> FindResumableRunAsync(string excelFilePath, Guid templateId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<BatchRun>(@"
            SELECT * FROM BatchRuns
            WHERE ExcelFilePath = @ExcelFilePath AND TemplateId = @TemplateId AND Status != @Completed
            ORDER BY StartedAt DESC
            LIMIT 1",
            new { ExcelFilePath = excelFilePath, TemplateId = templateId.ToString(), Completed = BatchRunStatus.Completed });
    }

    public async Task<IReadOnlySet<int>> GetSentRowNumbersAsync(Guid batchId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.QueryAsync<int>(
            "SELECT RowNumber FROM EmailLogs WHERE BatchId = @BatchId AND Status = @Status",
            new { BatchId = batchId.ToString(), Status = EmailLogStatus.Sent });
        return rows.ToHashSet();
    }

    public async Task UpsertEmailLogAsync(EmailLog log)
    {
        if (log.Id == Guid.Empty)
        {
            log.Id = Guid.NewGuid();
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO EmailLogs (Id, BatchId, RowNumber, Recipient, Subject, Status, Attempts, ErrorMessage, GmailMessageId, SentAt)
            VALUES (@Id, @BatchId, @RowNumber, @Recipient, @Subject, @Status, @Attempts, @ErrorMessage, @GmailMessageId, @SentAt)
            ON CONFLICT(BatchId, RowNumber) DO UPDATE SET
                Recipient = excluded.Recipient,
                Subject = excluded.Subject,
                Status = excluded.Status,
                Attempts = excluded.Attempts,
                ErrorMessage = excluded.ErrorMessage,
                GmailMessageId = excluded.GmailMessageId,
                SentAt = excluded.SentAt",
            new
            {
                Id = log.Id.ToString(),
                BatchId = log.BatchId.ToString(),
                log.RowNumber,
                log.Recipient,
                log.Subject,
                log.Status,
                log.Attempts,
                log.ErrorMessage,
                log.GmailMessageId,
                log.SentAt,
            });
    }

    public async Task<int> CountSentSinceAsync(DateTime sinceUtc)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EmailLogs WHERE Status = @Status AND SentAt >= @SinceUtc",
            new { Status = EmailLogStatus.Sent, SinceUtc = sinceUtc });
    }

    public async Task<IEnumerable<EmailLog>> GetLogsForBatchAsync(Guid batchId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<EmailLog>(
            "SELECT * FROM EmailLogs WHERE BatchId = @BatchId ORDER BY RowNumber",
            new { BatchId = batchId.ToString() });
    }

    public async Task<IEnumerable<BatchRun>> GetRecentRunsAsync(int take)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<BatchRun>(
            "SELECT * FROM BatchRuns ORDER BY StartedAt DESC LIMIT @Take",
            new { Take = take });
    }

    public async Task<(int TotalSuccess, int TotalFailure)> GetOverallEmailCountsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var success = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EmailLogs WHERE Status = @Status", new { Status = EmailLogStatus.Sent });
        var failure = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EmailLogs WHERE Status = @Status", new { Status = EmailLogStatus.Failed });
        return (success, failure);
    }
}
