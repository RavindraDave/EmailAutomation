using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using EmailAutomation.Application.Services;
using EmailAutomation.Infrastructure.Database;

namespace EmailAutomation.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentViewModel;

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public MainWindowViewModel()
    {
        _currentViewModel = new DashboardViewModel();
    }

    public void NavigateToDashboard()
    {
        CurrentViewModel = new DashboardViewModel();
    }

    public void NavigateToTemplates()
    {
        var repo = Program.Services?.GetService<IRepository>();
        if (repo != null)
        {
            CurrentViewModel = new TemplateManagementViewModel(repo);
        }
    }

    public void NavigateToBatch()
    {
        var batchService = Program.Services?.GetService<BatchExecutionService>();
        var repo = Program.Services?.GetService<IRepository>();
        if (batchService != null && repo != null)
        {
            CurrentViewModel = new BatchExecutionViewModel(batchService, repo);
        }
    }
}
