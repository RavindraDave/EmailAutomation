using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Dapper;

namespace EmailAutomation.Infrastructure.Database;

/// <summary>
/// Creates/upgrades the SQLite schema using PRAGMA user_version as a migration marker, so
/// existing users' databases (with an older BatchRuns/EmailLogs shape) get upgraded in place
/// instead of relying on bare CREATE TABLE IF NOT EXISTS, which never adds new columns.
/// </summary>
public class DatabaseInitializer
{
    private const int CurrentSchemaVersion = 2;

    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dbFile = builder.DataSource;
        if (!string.IsNullOrEmpty(dbFile) && !File.Exists(dbFile))
        {
            var directory = Path.GetDirectoryName(dbFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.Create(dbFile).Close();
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var version = connection.ExecuteScalar<long>("PRAGMA user_version;");

        using var transaction = connection.BeginTransaction();
        try
        {
            if (version < 1)
            {
                ApplyV1Baseline(connection, transaction);
                version = 1;
            }

            if (version < 2)
            {
                ApplyV2BatchTracking(connection, transaction);
                version = 2;
            }

            // PRAGMA does not support bound parameters; CurrentSchemaVersion is a compile-time
            // constant, not user input, so inlining it here is safe.
            connection.Execute($"PRAGMA user_version = {CurrentSchemaVersion};", transaction: transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void ApplyV1Baseline(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS EmailTemplates (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SubjectTemplate TEXT NOT NULL,
                BodyTemplate TEXT NOT NULL
            );", transaction: transaction);

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS BatchRuns (
                Id TEXT PRIMARY KEY,
                StartedAt DATETIME NOT NULL,
                CompletedAt DATETIME,
                Status TEXT NOT NULL
            );", transaction: transaction);

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS EmailLogs (
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
            );", transaction: transaction);
    }

    private static void ApplyV2BatchTracking(SqliteConnection connection, SqliteTransaction transaction)
    {
        AddColumnIfMissing(connection, transaction, "BatchRuns", "ExcelFilePath", "TEXT");
        AddColumnIfMissing(connection, transaction, "BatchRuns", "TemplateId", "TEXT");
        AddColumnIfMissing(connection, transaction, "BatchRuns", "TotalRows", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, transaction, "BatchRuns", "SentCount", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, transaction, "BatchRuns", "FailedCount", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, transaction, "BatchRuns", "SkippedCount", "INTEGER NOT NULL DEFAULT 0");

        // Makes resume idempotent: re-processing a row upserts its EmailLogs row instead of
        // inserting a duplicate.
        connection.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_emaillogs_batch_row ON EmailLogs(BatchId, RowNumber);",
            transaction: transaction);
    }

    private static void AddColumnIfMissing(SqliteConnection connection, SqliteTransaction transaction, string table, string column, string columnDefinition)
    {
        var existingColumns = connection.Query<string>(
            $"SELECT name FROM pragma_table_info('{table}');", transaction: transaction).ToList();

        if (!existingColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
        {
            connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition};", transaction: transaction);
        }
    }
}
