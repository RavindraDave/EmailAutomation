using System;
using System.Data;
using Dapper;

namespace EmailAutomation.Infrastructure.Database;

/// <summary>
/// SQLite has no native GUID column type - we store GUIDs as TEXT. Dapper does not automatically
/// convert TEXT to Guid for arbitrary providers (this was a latent bug: template loading via
/// GetTemplatesAsync/GetTemplateByIdAsync would throw InvalidCastException the first time it ran
/// against a real database, since nothing previously exercised SqliteRepository against a live DB).
/// This handler makes Guid properties round-trip transparently through Dapper.
/// </summary>
public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public static void Register()
    {
        // Dapper treats Guid as a type it already understands, so AddTypeHandler alone is ignored
        // for it unless the built-in type map is removed first.
        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.AddTypeHandler(typeof(Guid), new GuidTypeHandler());
    }

    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
    }

    public override Guid Parse(object value)
    {
        return value switch
        {
            string s => Guid.Parse(s),
            Guid g => g,
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType().Name ?? "null"} to Guid"),
        };
    }
}
