using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;

namespace EmailAutomation.Infrastructure.Database;

public class DatabaseInitializer
{
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

        var createTemplatesTable = @"
            CREATE TABLE IF NOT EXISTS EmailTemplates (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SubjectTemplate TEXT NOT NULL,
                BodyTemplate TEXT NOT NULL
            );";
        connection.Execute(createTemplatesTable);

        var createBatchRunsTable = @"
            CREATE TABLE IF NOT EXISTS BatchRuns (
                Id TEXT PRIMARY KEY,
                StartedAt DATETIME NOT NULL,
                CompletedAt DATETIME,
                Status TEXT NOT NULL
            );";
        connection.Execute(createBatchRunsTable);

        var createEmailLogsTable = @"
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
            );";
        connection.Execute(createEmailLogsTable);
    }
}
