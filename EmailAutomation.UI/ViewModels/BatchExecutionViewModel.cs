using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Database;

namespace EmailAutomation.UI.ViewModels;

public class BatchExecutionViewModel : ViewModelBase
{
    private readonly BatchExecutionService _batchService;
    private readonly IRepository _repository;

    public string Title => "Batch Execution";

    private string _excelFilePath = string.Empty;
    public string ExcelFilePath
    {
        get => _excelFilePath;
        set => SetProperty(ref _excelFilePath, value);
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
        set => SetProperty(ref _isRunning, value);
    }

    public ObservableCollection<EmailTemplate> Templates { get; } = new ObservableCollection<EmailTemplate>();

    private EmailTemplate? _selectedTemplate;
    public EmailTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public ICommand StartBatchCommand { get; }

    public BatchExecutionViewModel(BatchExecutionService batchService, IRepository repository)
    {
        _batchService = batchService;
        _repository = repository;

        StartBatchCommand = new RelayCommand(async () => await StartBatchAsync());

        _ = LoadTemplatesAsync();
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

    private async Task StartBatchAsync()
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(ExcelFilePath)) return;

        IsRunning = true;
        // The cancellation token would be passed here to allow Stop
        await _batchService.ExecuteBatchAsync(ExcelFilePath, SelectedTemplate, CancellationToken.None);
        IsRunning = false;
        ProgressValue = 100;
    }

    public void PauseBatch() { /* Pause logic */ }
    public void ResumeBatch() { /* Resume logic */ }
    public void StopBatch() { /* Stop logic */ }
}
