namespace EmailAutomation.Application.Services;

/// <summary>Generates a ready-to-fill-in sample Excel workbook matching the columns this app expects,
/// so a new user doesn't have to guess the required format from the README alone.</summary>
public interface ISampleTemplateGenerator
{
    void GenerateSampleWorkbook(string destinationPath);
}
