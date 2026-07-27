using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Reporting;

/// <summary>Writes a batch run's per-row results as RFC 4180 CSV. Recipient addresses and error
/// messages are free text and routinely contain commas/quotes/newlines, so correct quoting matters.</summary>
public static class CsvReportWriter
{
    private static readonly string[] Header = { "RowNumber", "Recipient", "Subject", "Status", "Attempts", "ErrorMessage", "GmailMessageId", "SentAt" };

    public static void WriteReport(IEnumerable<EmailLog> logs, TextWriter writer)
    {
        writer.Write(string.Join(",", Header));
        writer.Write("\r\n");

        foreach (var log in logs)
        {
            var fields = new[]
            {
                log.RowNumber.ToString(CultureInfo.InvariantCulture),
                log.Recipient,
                log.Subject ?? string.Empty,
                log.Status,
                log.Attempts.ToString(CultureInfo.InvariantCulture),
                log.ErrorMessage ?? string.Empty,
                log.GmailMessageId ?? string.Empty,
                log.SentAt?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            };

            writer.Write(string.Join(",", System.Array.ConvertAll(fields, Escape)));
            writer.Write("\r\n");
        }
    }

    private static readonly char[] FormulaTriggerChars = { '=', '+', '-', '@', '\t' };

    private static string Escape(string value)
    {
        // Excel/LibreOffice treat a leading =, +, -, @, or tab as the start of a formula. Recipient
        // addresses come straight from the input Excel file, so a crafted cell like
        // "=cmd|'/c calc'!A1" must not survive into the export unescaped (CSV/formula injection,
        // CWE-1236). Prefixing with a single quote neutralizes it while keeping the value readable.
        if (value.Length > 0 && FormulaTriggerChars.Contains(value[0]))
        {
            value = "'" + value;
        }

        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
