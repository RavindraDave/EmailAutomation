using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EmailAutomation.Tests;

public class DatabaseMigrationTests
{
    [Fact]
    public void Initialize_UpgradesLegacyV1Schema_WithoutLosingData()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"migrate_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={dbPath}";
        try
        {
            // Simulate a database created by the original (pre-migration) DatabaseInitializer:
            // the old 4-column BatchRuns shape and no PRAGMA user_version set (defaults to 0).
            using (var connection = new SqliteConnection(connStr))
            {
                connection.Open();
                connection.Execute(@"
                    CREATE TABLE BatchRuns (
                        Id TEXT PRIMARY KEY,
                        StartedAt DATETIME NOT NULL,
                        CompletedAt DATETIME,
                        Status TEXT NOT NULL
                    );");
                connection.Execute(@"
                    CREATE TABLE EmailLogs (
                        Id TEXT PRIMARY KEY,
                        BatchId TEXT NOT NULL,
                        RowNumber INTEGER NOT NULL,
                        Recipient TEXT NOT NULL,
                        Subject TEXT,
                        Status TEXT NOT NULL,
                        Attempts INTEGER NOT NULL,
                        ErrorMessage TEXT,
                        GmailMessageId TEXT,
                        SentAt DATETIME
                    );");
                connection.Execute(@"
                    CREATE TABLE EmailTemplates (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        SubjectTemplate TEXT NOT NULL,
                        BodyTemplate TEXT NOT NULL
                    );");

                connection.Execute(
                    "INSERT INTO BatchRuns (Id, StartedAt, CompletedAt, Status) VALUES (@Id, @StartedAt, @CompletedAt, @Status)",
                    new { Id = Guid.NewGuid().ToString(), StartedAt = DateTime.UtcNow.AddDays(-1), CompletedAt = (DateTime?)null, Status = "Completed" });
            }

            new DatabaseInitializer(connStr).Initialize();

            using var verifyConnection = new SqliteConnection(connStr);
            verifyConnection.Open();

            var columns = verifyConnection.Query<string>("SELECT name FROM pragma_table_info('BatchRuns');").ToList();
            Assert.Contains("ExcelFilePath", columns);
            Assert.Contains("TemplateId", columns);
            Assert.Contains("TotalRows", columns);
            Assert.Contains("SentCount", columns);
            Assert.Contains("FailedCount", columns);
            Assert.Contains("SkippedCount", columns);

            var preservedCount = verifyConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM BatchRuns");
            Assert.Equal(1, preservedCount);

            var version = verifyConnection.ExecuteScalar<long>("PRAGMA user_version;");
            Assert.Equal(2, version);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public void Initialize_IsIdempotent_WhenRunTwice()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"migrate_idempotent_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={dbPath}";
        try
        {
            var initializer = new DatabaseInitializer(connStr);
            initializer.Initialize();
            initializer.Initialize(); // must not throw (duplicate column/index errors)

            using var connection = new SqliteConnection(connStr);
            connection.Open();
            var version = connection.ExecuteScalar<long>("PRAGMA user_version;");
            Assert.Equal(2, version);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}

public class SqliteRepositoryTests
{
    private static (string ConnStr, string DbPath) CreateTempDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"repo_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={dbPath}";
        new DatabaseInitializer(connStr).Initialize();
        return (connStr, dbPath);
    }

    [Fact]
    public async Task EmailTemplate_RoundTrip_SurvivesGuidColumnMapping()
    {
        // Regression test: SQLite has no native GUID type, so Id is stored as TEXT. Dapper does
        // not map TEXT -> Guid automatically for this provider, and nothing previously exercised
        // this path against a real database - GetTemplatesAsync/GetTemplateByIdAsync would throw
        // InvalidCastException the first time a real user loaded their templates.
        var (connStr, dbPath) = CreateTempDatabase();
        try
        {
            IRepository repository = new SqliteRepository(connStr);
            var template = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                Name = "Welcome",
                SubjectTemplate = "Hi {{FirstName}}",
                BodyTemplate = "Welcome aboard, {{FirstName}}!",
            };

            await repository.AddTemplateAsync(template);

            var all = (await repository.GetTemplatesAsync()).ToList();
            Assert.Single(all);
            Assert.Equal(template.Id, all[0].Id);

            var byId = await repository.GetTemplateByIdAsync(template.Id);
            Assert.NotNull(byId);
            Assert.Equal("Welcome", byId!.Name);

            template.Name = "Welcome (updated)";
            await repository.UpdateTemplateAsync(template);
            var updated = await repository.GetTemplateByIdAsync(template.Id);
            Assert.Equal("Welcome (updated)", updated!.Name);

            await repository.DeleteTemplateAsync(template.Id);
            Assert.Null(await repository.GetTemplateByIdAsync(template.Id));
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task BatchRunAndEmailLog_RoundTrip_SupportsResumeAndReporting()
    {
        var (connStr, dbPath) = CreateTempDatabase();
        try
        {
            IRepository repository = new SqliteRepository(connStr);
            var templateId = Guid.NewGuid();
            const string excelPath = "/tmp/recipients.xlsx";

            var run = new BatchRun
            {
                ExcelFilePath = excelPath,
                TemplateId = templateId,
                StartedAt = DateTime.UtcNow,
                Status = BatchRunStatus.Running,
                TotalRows = 3,
            };
            var batchId = await repository.CreateBatchRunAsync(run);

            await repository.UpsertEmailLogAsync(new EmailLog { BatchId = batchId, RowNumber = 1, Recipient = "a@example.com", Status = EmailLogStatus.Sent, SentAt = DateTime.UtcNow, Attempts = 1 });
            await repository.UpsertEmailLogAsync(new EmailLog { BatchId = batchId, RowNumber = 2, Recipient = "b@example.com", Status = EmailLogStatus.Failed, ErrorMessage = "bounced", Attempts = 3 });

            // Interrupted here (row 3 never processed) - the run should be offered for resume.
            var resumable = await repository.FindResumableRunAsync(excelPath, templateId);
            Assert.NotNull(resumable);
            Assert.Equal(batchId, resumable!.Id);

            var sentRows = await repository.GetSentRowNumbersAsync(batchId);
            Assert.Contains(1, sentRows);
            Assert.DoesNotContain(2, sentRows);

            // Resume: row 3 sends, row 2 is retried and now succeeds - must update, not duplicate.
            await repository.UpsertEmailLogAsync(new EmailLog { BatchId = batchId, RowNumber = 3, Recipient = "c@example.com", Status = EmailLogStatus.Sent, SentAt = DateTime.UtcNow, Attempts = 1 });
            await repository.UpsertEmailLogAsync(new EmailLog { BatchId = batchId, RowNumber = 2, Recipient = "b@example.com", Status = EmailLogStatus.Sent, SentAt = DateTime.UtcNow, Attempts = 4 });

            var logs = (await repository.GetLogsForBatchAsync(batchId)).ToList();
            Assert.Equal(3, logs.Count); // the unique (BatchId, RowNumber) index keeps this at 3, not 4
            Assert.All(logs, l => Assert.Equal(EmailLogStatus.Sent, l.Status));

            await repository.CompleteBatchRunAsync(batchId, BatchRunStatus.Completed, DateTime.UtcNow, sentCount: 3, failedCount: 0, skippedCount: 0);

            var noLongerResumable = await repository.FindResumableRunAsync(excelPath, templateId);
            Assert.Null(noLongerResumable);

            var recent = (await repository.GetRecentRunsAsync(10)).ToList();
            Assert.Single(recent);
            Assert.Equal(BatchRunStatus.Completed, recent[0].Status);
            Assert.Equal(3, recent[0].SentCount);

            var (totalSuccess, totalFailure) = await repository.GetOverallEmailCountsAsync();
            Assert.Equal(3, totalSuccess);
            Assert.Equal(0, totalFailure);

            var sentToday = await repository.CountSentSinceAsync(DateTime.UtcNow.AddDays(-1));
            Assert.Equal(3, sentToday);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
