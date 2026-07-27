using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.UI.Services;

namespace EmailAutomation.UI.ViewModels;

public class BatchExecutionViewModel : ViewModelBase
{
    private readonly BatchExecutionService _batchService;
    private readonly BatchValidationService _validationService;
    private readonly IRepository _repository;
    private readonly IFilePickerService _filePickerService;
    private readonly ISampleTemplateGenerator _sampleTemplateGenerator;
    private readonly PauseTokenSource _pauseTokenSource = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Guid? _pendingResumeBatchId;

    public string Title => "Batch Execution";

    private string _excelFilePath = string.Empty;
    public string ExcelFilePath
    {
        get => _excelFilePath;
        set
        {
            if (SetProperty(ref _excelFilePath, value))
            {
                ResetValidation();
            }
        }
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanPause));
                OnPropertyChanged(nameof(CanResume));
                OnPropertyChanged(nameof(CanStart));
            }
        }
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(CanPause));
                OnPropertyChanged(nameof(CanResume));
            }
        }
    }

    public bool CanPause => IsRunning && !IsPaused;
    public bool CanResume => IsRunning && IsPaused;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _succeededCount;
    public int SucceededCount
    {
        get => _succeededCount;
        set => SetProperty(ref _succeededCount, value);
    }

    private int _failedCount;
    public int FailedCount
    {
        get => _failedCount;
        set => SetProperty(ref _failedCount, value);
    }

    private int _skippedCount;
    public int SkippedCount
    {
        get => _skippedCount;
        set => SetProperty(ref _skippedCount, value);
    }

    public ObservableCollection<EmailTemplate> Templates { get; } = new();

    private EmailTemplate? _selectedTemplate;
    public EmailTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                ResetValidation();
            }
        }
    }

    // --- Preview / validation ---

    private bool _isValidated;
    public bool IsValidated
    {
        get => _isValidated;
        set
        {
            if (SetProperty(ref _isValidated, value))
            {
                OnPropertyChanged(nameof(CanStart));
            }
        }
    }

    public bool CanStart => !IsRunning && IsValidated;

    private string _validationSummary = string.Empty;
    public string ValidationSummary
    {
        get => _validationSummary;
        set => SetProperty(ref _validationSummary, value);
    }

    public ObservableCollection<string> ValidationIssues { get; } = new();
    public ObservableCollection<RowPreview> PreviewRows { get; } = new();

    // --- Resume prompt ---

    private bool _hasPendingResumePrompt;
    public bool HasPendingResumePrompt
    {
        get => _hasPendingResumePrompt;
        set => SetProperty(ref _hasPendingResumePrompt, value);
    }

    public ICommand StartBatchCommand { get; }
    public ICommand PauseBatchCommand { get; }
    public ICommand ResumeBatchCommand { get; }
    public ICommand StopBatchCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ResumePreviousRunCommand { get; }
    public ICommand StartFreshCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand DownloadSampleTemplateCommand { get; }

    public BatchExecutionViewModel(
        BatchExecutionService batchService,
        BatchValidationService validationService,
        IRepository repository,
        IFilePickerService filePickerService,
        ISampleTemplateGenerator sampleTemplateGenerator)
    {
        _batchService = batchService;
        _validationService = validationService;
        _repository = repository;
        _filePickerService = filePickerService;
        _sampleTemplateGenerator = sampleTemplateGenerator;

        PreviewCommand = new RelayCommand(RunPreview, () => !IsRunning);
        StartBatchCommand = new RelayCommand(async () => await StartBatchAsync(), () => !IsRunning);
        PauseBatchCommand = new RelayCommand(PauseBatch, () => CanPause);
        ResumeBatchCommand = new RelayCommand(ResumeBatch, () => CanResume);
        StopBatchCommand = new RelayCommand(StopBatch, () => IsRunning);
        ResumePreviousRunCommand = new RelayCommand(async () => await ResumePreviousRunAsync());
        StartFreshCommand = new RelayCommand(async () => await StartFreshRunAsync());
        BrowseCommand = new RelayCommand(async () => await BrowseForExcelFileAsync(), () => !IsRunning);
        DownloadSampleTemplateCommand = new RelayCommand(async () => await DownloadSampleTemplateAsync());

        _ = LoadTemplatesAsync();
    }

    private async Task BrowseForExcelFileAsync()
    {
        var path = await _filePickerService.PickOpenExcelFileAsync("Select Recipients Excel File");
        if (!string.IsNullOrEmpty(path))
        {
            ExcelFilePath = path;
        }
    }

    private async Task DownloadSampleTemplateAsync()
    {
        var path = await _filePickerService.PickSaveExcelFileAsync("Save Sample Excel Template", "EmailAutomation_SampleTemplate.xlsx");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            _sampleTemplateGenerator.GenerateSampleWorkbook(path);
            StatusMessage = $"Sample template saved to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save the sample template: {ex.Message}";
        }
    }

    private async Task LoadTemplatesAsync()
    {
        var templates = await _repository.GetTemplatesAsync();
        Templates.Clear();
        foreach (var t in templates)
        {
            Templates.Add(t);
        }
    }

    private void ResetValidation()
    {
        IsValidated = false;
        ValidationSummary = string.Empty;
        ValidationIssues.Clear();
        PreviewRows.Clear();
        HasPendingResumePrompt = false;
    }

    private void RunPreview()
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(ExcelFilePath))
        {
            StatusMessage = "Choose an Excel file and a template first.";
            return;
        }

        ValidationIssues.Clear();
        PreviewRows.Clear();

        ValidationReport report;
        try
        {
            report = _validationService.Validate(ExcelFilePath, SelectedTemplate);
        }
        catch (Exception ex)
        {
            IsValidated = false;
            ValidationSummary = $"Could not validate: {ex.Message}";
            return;
        }

        foreach (var issue in report.Issues)
        {
            ValidationIssues.Add(issue.RowNumber > 0 ? $"Row {issue.RowNumber}: {issue.Message}" : issue.Message);
        }

        foreach (var preview in report.Previews)
        {
            PreviewRows.Add(preview);
        }

        IsValidated = !report.HasBlockingErrors;
        ValidationSummary = report.HasBlockingErrors
            ? $"{report.Issues.Count(i => i.Severity == ValidationSeverity.Error)} problem(s) found - fix these before sending."
            : $"Looks good: {report.ValidRows} of {report.TotalRows} row(s) ready to send.";
    }

    private async Task StartBatchAsync()
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(ExcelFilePath))
        {
            StatusMessage = "Choose an Excel file and a template before starting.";
            return;
        }

        if (!IsValidated)
        {
            StatusMessage = "Click \"Preview / Validate\" first and resolve any problems before sending.";
            return;
        }

        var resumable = await _repository.FindResumableRunAsync(ExcelFilePath, SelectedTemplate.Id);
        if (resumable != null)
        {
            _pendingResumeBatchId = resumable.Id;
            HasPendingResumePrompt = true;
            StatusMessage = $"An interrupted run of this file was found ({resumable.SentCount} already sent). Resume it, or start fresh?";
            return;
        }

        await RunBatchAsync(resumeBatchId: null);
    }

    private async Task ResumePreviousRunAsync()
    {
        var resumeId = _pendingResumeBatchId;
        _pendingResumeBatchId = null;
        HasPendingResumePrompt = false;
        await RunBatchAsync(resumeId);
    }

    private async Task StartFreshRunAsync()
    {
        _pendingResumeBatchId = null;
        HasPendingResumePrompt = false;
        await RunBatchAsync(resumeBatchId: null);
    }

    private async Task RunBatchAsync(Guid? resumeBatchId)
    {
        if (SelectedTemplate == null)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        IsRunning = true;
        IsPaused = false;
        ProgressValue = 0;
        SucceededCount = 0;
        FailedCount = 0;
        SkippedCount = 0;
        StatusMessage = "Starting...";

        var progress = new Progress<BatchProgress>(p =>
        {
            ProgressValue = p.Total > 0 ? (int)(p.Processed * 100.0 / p.Total) : 0;
            SucceededCount = p.Succeeded;
            FailedCount = p.Failed;
            SkippedCount = p.Skipped;
            StatusMessage = string.IsNullOrEmpty(p.CurrentRecipient)
                ? $"Processed {p.Processed} of {p.Total}"
                : $"Row {p.Processed} of {p.Total}: {p.CurrentRecipient}";
        });

        try
        {
            var request = new BatchRequest { ExcelFilePath = ExcelFilePath, Template = SelectedTemplate, ResumeBatchId = resumeBatchId };
            var summary = await _batchService.ExecuteBatchAsync(
                request, progress, _pauseTokenSource.Token, _cancellationTokenSource.Token);

            ProgressValue = 100;
            StatusMessage = $"{summary.Status}: {summary.SentCount} sent, {summary.FailedCount} failed, {summary.SkippedCount} skipped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Batch failed to start: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void PauseBatch()
    {
        _pauseTokenSource.Pause();
        IsPaused = true;
        StatusMessage = "Paused.";
    }

    private void ResumeBatch()
    {
        _pauseTokenSource.Resume();
        IsPaused = false;
    }

    private void StopBatch()
    {
        _cancellationTokenSource?.Cancel();
        // A cancellation requested while paused would otherwise wait on the pause gate forever.
        _pauseTokenSource.Resume();
        StatusMessage = "Stopping...";
    }
}
