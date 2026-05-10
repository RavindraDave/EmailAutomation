using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Infrastructure.Excel;

public class ClosedXmlExcelReader : IExcelReader
{
    public IEnumerable<EmailJob> ReadJobs(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found", filePath);

        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed();
        var jobs = new List<EmailJob>();

        var headers = new Dictionary<int, string>();
        bool isFirstRow = true;

        foreach (var row in rows)
        {
            if (isFirstRow)
            {
                foreach (var cell in row.CellsUsed())
                {
                    headers[cell.Address.ColumnNumber] = cell.GetString();
                }
                isFirstRow = false;
                continue;
            }

            var job = new EmailJob
            {
                RowNumber = row.RowNumber()
            };

            bool isEnabled = true;

            foreach (var cell in row.CellsUsed())
            {
                var colNum = cell.Address.ColumnNumber;
                if (!headers.TryGetValue(colNum, out var colName)) continue;

                var value = cell.GetString();

                if (colName.Equals("To", StringComparison.OrdinalIgnoreCase))
                {
                    job.To = value;
                }
                else if (colName.Equals("Cc", StringComparison.OrdinalIgnoreCase))
                {
                    job.Cc = value;
                }
                else if (colName.Equals("Subject", StringComparison.OrdinalIgnoreCase))
                {
                    job.Subject = value;
                }
                else if (colName.Equals("AttachmentPath", StringComparison.OrdinalIgnoreCase))
                {
                    job.AttachmentPath = value;
                }
                else if (colName.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out var enabled))
                    {
                        isEnabled = enabled;
                    }
                }

                job.Variables[colName] = value;
            }

            if (isEnabled && !string.IsNullOrWhiteSpace(job.To))
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }
}
