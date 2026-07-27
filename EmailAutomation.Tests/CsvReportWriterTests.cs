using System;
using System.Collections.Generic;
using System.IO;
using EmailAutomation.Application.Reporting;
using EmailAutomation.Domain.Models;
using Xunit;

namespace EmailAutomation.Tests;

public class CsvReportWriterTests
{
    [Fact]
    public void WriteReport_QuotesFieldsWithCommasQuotesAndNewlines()
    {
        var logs = new List<EmailLog>
        {
            new()
            {
                RowNumber = 1,
                Recipient = "a@example.com",
                Subject = "Hello, Ann",
                Status = EmailLogStatus.Failed,
                Attempts = 3,
                ErrorMessage = "SMTP said: \"mailbox unavailable\"\nretrying later",
            },
        };

        using var writer = new StringWriter();
        CsvReportWriter.WriteReport(logs, writer);
        var csv = writer.ToString();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("RowNumber,Recipient,Subject,Status,Attempts,ErrorMessage,GmailMessageId,SentAt", lines[0]);

        // The comma-containing Subject must be quoted; the ErrorMessage's embedded quote must be
        // doubled and its embedded newline preserved verbatim inside quotes (not split across rows).
        Assert.Contains("\"Hello, Ann\"", lines[1]);
        Assert.Contains("\"SMTP said: \"\"mailbox unavailable\"\"\nretrying later\"", csv);

        // A naive split on "\r\n" would see the embedded newline as a spurious extra line -
        // parse it back properly to confirm there's really only one data row.
        Assert.Equal(2, CountCsvRecords(csv));
    }

    [Fact]
    public void WriteReport_LeavesPlainFieldsUnquoted()
    {
        var logs = new List<EmailLog>
        {
            new() { RowNumber = 1, Recipient = "plain@example.com", Status = EmailLogStatus.Sent, Attempts = 1, GmailMessageId = "abc123", SentAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) },
        };

        using var writer = new StringWriter();
        CsvReportWriter.WriteReport(logs, writer);
        var csv = writer.ToString();

        Assert.Contains("1,plain@example.com,,Sent,1,,abc123,", csv);
        Assert.DoesNotContain("\"plain@example.com\"", csv);
    }

    [Fact]
    public void WriteReport_NeutralizesLeadingFormulaCharacters()
    {
        // Excel/LibreOffice treat a leading =, +, -, @, or tab as the start of a formula. Recipient
        // comes straight from the input Excel file, so a crafted cell must not survive into the
        // export unescaped (CSV/formula injection, CWE-1236).
        var logs = new List<EmailLog>
        {
            new() { RowNumber = 1, Recipient = "=cmd|'/c calc'!A1", Status = EmailLogStatus.Sent, Attempts = 1 },
        };

        using var writer = new StringWriter();
        CsvReportWriter.WriteReport(logs, writer);
        var csv = writer.ToString();

        Assert.Contains("'=cmd|'/c calc'!A1", csv);
        Assert.DoesNotContain("1,=cmd", csv);
    }

    [Fact]
    public void WriteReport_HandlesEmptyLogList()
    {
        using var writer = new StringWriter();
        CsvReportWriter.WriteReport(new List<EmailLog>(), writer);
        var csv = writer.ToString();

        Assert.Equal("RowNumber,Recipient,Subject,Status,Attempts,ErrorMessage,GmailMessageId,SentAt\r\n", csv);
    }

    /// <summary>Minimal RFC 4180-aware record counter: a naive line-split would miscount when a
    /// quoted field contains a raw newline, which is exactly the case this test needs to catch.</summary>
    private static int CountCsvRecords(string csv)
    {
        var records = 0;
        var inQuotes = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '\n' && !inQuotes)
            {
                records++;
            }
        }
        return records;
    }
}
