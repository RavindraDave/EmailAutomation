using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Infrastructure.Database;

public interface IRepository
{
    Task<IEnumerable<EmailTemplate>> GetTemplatesAsync();
    Task<EmailTemplate?> GetTemplateByIdAsync(Guid id);
    Task AddTemplateAsync(EmailTemplate template);
    Task UpdateTemplateAsync(EmailTemplate template);
    Task DeleteTemplateAsync(Guid id);
}

public class SqliteRepository : IRepository
{
    private readonly string _connectionString;

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
}
