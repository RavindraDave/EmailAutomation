using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.UI.Services;

namespace EmailAutomation.UI.ViewModels;

public class TemplateManagementViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IFilePickerService _filePickerService;
    private readonly ITemplateEngine _templateEngine;
    public string Title => "Template Management";

    public ObservableCollection<EmailTemplate> Templates { get; } = new ObservableCollection<EmailTemplate>();

    private EmailTemplate? _selectedTemplate;
    public EmailTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    // A simple DelegateCommand since Avalonia templates don't bundle one by default
    public ICommand NewTemplateCommand { get; }
    public ICommand SaveTemplateCommand { get; }
    public ICommand LoadHtmlFileCommand { get; }
    public ICommand PreviewInBrowserCommand { get; }

    public TemplateManagementViewModel(IRepository repository, IFilePickerService filePickerService, ITemplateEngine templateEngine)
    {
        _repository = repository;
        _filePickerService = filePickerService;
        _templateEngine = templateEngine;
        NewTemplateCommand = new RelayCommand(NewTemplate);
        SaveTemplateCommand = new RelayCommand(async () => await SaveTemplateAsync());
        LoadHtmlFileCommand = new RelayCommand(async () => await LoadHtmlFileAsync());
        PreviewInBrowserCommand = new RelayCommand(PreviewInBrowser);

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

    private void NewTemplate()
    {
        var t = new EmailTemplate { Id = Guid.NewGuid(), Name = "New Template" };
        Templates.Add(t);
        SelectedTemplate = t;
    }

    private async Task SaveTemplateAsync()
    {
        if (SelectedTemplate == null) return;

        var existing = await _repository.GetTemplateByIdAsync(SelectedTemplate.Id);
        if (existing == null)
        {
            await _repository.AddTemplateAsync(SelectedTemplate);
        }
        else
        {
            await _repository.UpdateTemplateAsync(SelectedTemplate);
        }
    }

    // internal (not private) so tests can await this directly - RelayCommand fires async work
    // without exposing a Task, so the command itself can't be awaited from a test.
    internal async Task LoadHtmlFileAsync()
    {
        if (SelectedTemplate == null) return;

        var path = await _filePickerService.PickOpenHtmlFileAsync("Load HTML Email Body");
        if (path == null) return;

        SelectedTemplate.BodyTemplate = await File.ReadAllTextAsync(path);
    }

    // Renders the current body (placeholders left blank) to a temp file and opens it in the
    // system browser - the simplest way to preview real HTML/table formatting without embedding
    // a rendering engine in the app itself.
    private void PreviewInBrowser()
    {
        var html = RenderPreviewHtml();
        if (html == null) return;

        var previewPath = Path.Combine(Path.GetTempPath(), $"email-preview-{Guid.NewGuid():N}.html");
        File.WriteAllText(previewPath, html);

        Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true });
    }

    // Split from PreviewInBrowser (internal, not private) so tests can verify the rendering step
    // - e.g. that table markup survives untouched - without launching a real browser process.
    internal string? RenderPreviewHtml()
    {
        if (SelectedTemplate == null) return null;

        return _templateEngine.Render(SelectedTemplate.BodyTemplate, new Dictionary<string, string>());
    }
}
