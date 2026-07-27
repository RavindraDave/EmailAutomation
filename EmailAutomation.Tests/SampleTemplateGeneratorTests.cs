using System;
using System.IO;
using System.Linq;
using EmailAutomation.Infrastructure.Excel;
using Xunit;

namespace EmailAutomation.Tests;

public class SampleTemplateGeneratorTests
{
    [Fact]
    public void GenerateSampleWorkbook_ProducesFile_ThatTheAppsOwnExcelReaderCanParse()
    {
        // The point of shipping a sample template is that a user can fill it in and load it
        // straight back into this app - so the real regression is round-tripping it through
        // ClosedXmlExcelReader, not just checking ClosedXML wrote *a* file.
        var path = Path.Combine(Path.GetTempPath(), $"sample_{Guid.NewGuid():N}.xlsx");
        try
        {
            new ClosedXmlSampleTemplateGenerator().GenerateSampleWorkbook(path);

            Assert.True(File.Exists(path));

            var jobs = new ClosedXmlExcelReader().ReadJobs(path).ToList();

            // One example row is IsEnabled=false and must be filtered out by the reader.
            Assert.Equal(2, jobs.Count);
            Assert.Contains(jobs, j => j.To == "jane.doe@example.com");
            Assert.Contains(jobs, j => j.To == "john.smith@example.com");
            Assert.DoesNotContain(jobs, j => j.To == "disabled.example@example.com");

            var jane = jobs.Single(j => j.To == "jane.doe@example.com");
            Assert.Equal("Jane", jane.Variables["FirstName"]);
            Assert.Equal("INV-1001", jane.Variables["InvoiceNo"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GenerateSampleWorkbook_CreatesParentDirectory_WhenMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sample_dir_{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "template.xlsx");
        try
        {
            new ClosedXmlSampleTemplateGenerator().GenerateSampleWorkbook(path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
