using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAutomation.Application.Reporting;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.UI.Services;

namespace EmailAutomation.UI.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IFilePickerService _filePickerService;

    public string Title => "Dashboard";

    private int _totalSuccess;
    public int TotalSuccess
    {
        get => _totalSuccess;
        set => SetProperty(ref _totalSuccess, value);
    }

    private int _totalFailures;
    public int TotalFailures
    {
        get => _totalFailures;
        set => SetProperty(ref _totalFailures, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<BatchRun> RecentRuns { get; } = new();

    private BatchRun? _selectedRun;
    public BatchRun? SelectedRun
    {
        get => _selectedRun;
        set => SetProperty(ref _selectedRun, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExportReportCommand { get; }

    public DashboardViewModel(IRepository repository, IFilePickerService filePickerService)
    {
        _repository = repository;
        _filePickerService = filePickerService;

        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        ExportReportCommand = new RelayCommand(async () => await ExportReportAsync());

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var (success, failure) = await _repository.GetOverallEmailCountsAsync();
        TotalSuccess = success;
        TotalFailures = failure;

        var runs = await _repository.GetRecentRunsAsync(20);
        RecentRuns.Clear();
        foreach (var run in runs)
        {
            RecentRuns.Add(run);
        }
    }

    private async Task ExportReportAsync()
    {
        if (SelectedRun == null)
        {
            StatusMessage = "Select a run from the list first.";
            return;
        }

        var suggestedName = $"EmailReport_{SelectedRun.StartedAt:yyyyMMdd_HHmmss}.csv";
        var path = await _filePickerService.PickSaveCsvFileAsync("Save Batch Report", suggestedName);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var logs = await _repository.GetLogsForBatchAsync(SelectedRun.Id);
            await using var writer = new StreamWriter(path);
            CsvReportWriter.WriteReport(logs, writer);
            StatusMessage = $"Report saved to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save the report: {ex.Message}";
        }
    }
}
