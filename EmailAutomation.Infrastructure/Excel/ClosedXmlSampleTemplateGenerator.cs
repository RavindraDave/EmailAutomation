using System.IO;
using ClosedXML.Excel;
using EmailAutomation.Application.Services;

namespace EmailAutomation.Infrastructure.Excel;

public class ClosedXmlSampleTemplateGenerator : ISampleTemplateGenerator
{
    private static readonly string[] Headers = { "To", "Cc", "Subject", "AttachmentPath", "IsEnabled", "FirstName", "InvoiceNo" };

    public void GenerateSampleWorkbook(string destinationPath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Recipients");

        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xEE, 0xF7);
        }

        // A couple of filled-in example rows so the expected format is self-explanatory -
        // To/IsEnabled are required, everything else (Cc, Subject, AttachmentPath, and any
        // custom column like FirstName/InvoiceNo) is optional and usable in templates as {{ColumnName}}.
        AddExampleRow(worksheet, 2, "jane.doe@example.com", "", "", "", true, "Jane", "INV-1001");
        AddExampleRow(worksheet, 3, "john.smith@example.com", "", "", "", true, "John", "INV-1002");
        AddExampleRow(worksheet, 4, "disabled.example@example.com", "", "", "", false, "Skip Me", "INV-1003");

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        workbook.SaveAs(destinationPath);
    }

    private static void AddExampleRow(IXLWorksheet worksheet, int row, string to, string cc, string subject, string attachmentPath, bool isEnabled, string firstName, string invoiceNo)
    {
        worksheet.Cell(row, 1).Value = to;
        worksheet.Cell(row, 2).Value = cc;
        worksheet.Cell(row, 3).Value = subject;
        worksheet.Cell(row, 4).Value = attachmentPath;
        worksheet.Cell(row, 5).Value = isEnabled;
        worksheet.Cell(row, 6).Value = firstName;
        worksheet.Cell(row, 7).Value = invoiceNo;
    }
}
