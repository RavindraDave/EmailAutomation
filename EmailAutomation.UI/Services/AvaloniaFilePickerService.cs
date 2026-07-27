using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace EmailAutomation.UI.Services;

public class AvaloniaFilePickerService : IFilePickerService
{
    private static readonly FilePickerFileType ExcelFileType = new("Excel Workbook")
    {
        Patterns = new[] { "*.xlsx" },
    };

    private static readonly FilePickerFileType CsvFileType = new("CSV File")
    {
        Patterns = new[] { "*.csv" },
    };

    public async Task<string?> PickOpenExcelFileAsync(string title)
    {
        var provider = GetStorageProvider();
        if (provider is null)
        {
            return null;
        }

        var results = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { ExcelFileType },
        });

        return results.Count > 0 ? results[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveExcelFileAsync(string title, string suggestedFileName)
    {
        var provider = GetStorageProvider();
        if (provider is null)
        {
            return null;
        }

        var result = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "xlsx",
            FileTypeChoices = new List<FilePickerFileType> { ExcelFileType },
        });

        return result?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveCsvFileAsync(string title, string suggestedFileName)
    {
        var provider = GetStorageProvider();
        if (provider is null)
        {
            return null;
        }

        var result = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "csv",
            FileTypeChoices = new List<FilePickerFileType> { CsvFileType },
        });

        return result?.TryGetLocalPath();
    }

    private static IStorageProvider? GetStorageProvider()
    {
        // Fully qualified: EmailAutomation.Application (this solution's Application project) would
        // otherwise shadow Avalonia.Application for an unqualified "Application" reference here.
        return global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow?.StorageProvider
            : null;
    }
}
