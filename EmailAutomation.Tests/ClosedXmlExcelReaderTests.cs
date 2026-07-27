using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using EmailAutomation.Infrastructure.Excel;
using Xunit;

namespace EmailAutomation.Tests;

public class ClosedXmlExcelReaderTests
{
    private static string WriteWorkbook(Action<IXLWorksheet> populate)
    {
        var path = Path.Combine(Path.GetTempPath(), $"reader_{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        populate(worksheet);
        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public void ReadJobs_ExcludesRowsWithIsEnabledFalse()
    {
        var path = WriteWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "To";
            ws.Cell(1, 2).Value = "IsEnabled";
            ws.Cell(2, 1).Value = "a@example.com";
            ws.Cell(2, 2).Value = true;
            ws.Cell(3, 1).Value = "b@example.com";
            ws.Cell(3, 2).Value = false;
        });
        try
        {
            var jobs = new ClosedXmlExcelReader().ReadJobs(path).ToList();

            Assert.Single(jobs);
            Assert.Equal("a@example.com", jobs[0].To);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadJobs_SkipsRowsWithNoRecipient()
    {
        var path = WriteWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "To";
            ws.Cell(2, 1).Value = "";
            ws.Cell(3, 1).Value = "b@example.com";
        });
        try
        {
            var jobs = new ClosedXmlExcelReader().ReadJobs(path).ToList();

            Assert.Single(jobs);
            Assert.Equal("b@example.com", jobs[0].To);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadJobs_ExposesCustomColumns_AsTemplateVariables()
    {
        var path = WriteWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "To";
            ws.Cell(1, 2).Value = "FirstName";
            ws.Cell(1, 3).Value = "InvoiceNo";
            ws.Cell(2, 1).Value = "a@example.com";
            ws.Cell(2, 2).Value = "Ann";
            ws.Cell(2, 3).Value = "INV-42";
        });
        try
        {
            var jobs = new ClosedXmlExcelReader().ReadJobs(path).ToList();

            Assert.Single(jobs);
            Assert.Equal("Ann", jobs[0].Variables["FirstName"]);
            Assert.Equal("INV-42", jobs[0].Variables["InvoiceNo"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadJobs_MapsKnownColumns_ToDedicatedProperties()
    {
        var path = WriteWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "To";
            ws.Cell(1, 2).Value = "Cc";
            ws.Cell(1, 3).Value = "Subject";
            ws.Cell(1, 4).Value = "AttachmentPath";
            ws.Cell(2, 1).Value = "a@example.com";
            ws.Cell(2, 2).Value = "cc@example.com";
            ws.Cell(2, 3).Value = "Row-specific subject";
            ws.Cell(2, 4).Value = "/tmp/file.pdf";
        });
        try
        {
            var job = new ClosedXmlExcelReader().ReadJobs(path).Single();

            Assert.Equal("a@example.com", job.To);
            Assert.Equal("cc@example.com", job.Cc);
            Assert.Equal("Row-specific subject", job.Subject);
            Assert.Equal("/tmp/file.pdf", job.AttachmentPath);
            Assert.Equal(2, job.RowNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadJobs_MissingFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => new ClosedXmlExcelReader().ReadJobs("/nonexistent/path.xlsx").ToList());
    }
}
