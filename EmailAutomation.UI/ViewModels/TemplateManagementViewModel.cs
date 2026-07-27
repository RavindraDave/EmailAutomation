using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.UI.ViewModels;

public class TemplateManagementViewModel : ViewModelBase
{
    private readonly IRepository _repository;
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

    public TemplateManagementViewModel(IRepository repository)
    {
        _repository = repository;
        NewTemplateCommand = new RelayCommand(NewTemplate);
        SaveTemplateCommand = new RelayCommand(async () => await SaveTemplateAsync());

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
}
