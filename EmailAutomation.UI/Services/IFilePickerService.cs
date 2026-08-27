using System.Threading.Tasks;

namespace EmailAutomation.UI.Services;

/// <summary>Wraps Avalonia's IStorageProvider so ViewModels can ask for a file path without
/// depending directly on Avalonia UI types, keeping them easier to unit test.</summary>
public interface IFilePickerService
{
    Task<string?> PickOpenExcelFileAsync(string title);
    Task<string?> PickSaveExcelFileAsync(string title, string suggestedFileName);
    Task<string?> PickSaveCsvFileAsync(string title, string suggestedFileName);
    Task<string?> PickOpenHtmlFileAsync(string title);
}
