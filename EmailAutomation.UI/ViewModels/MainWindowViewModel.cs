using System;
using Microsoft.Extensions.DependencyInjection;

namespace EmailAutomation.UI.ViewModels;

/// <summary>
/// Resolves child ViewModels from the DI container on navigation ("ViewModel locator" pattern) -
/// this is the one place an IServiceProvider is used directly, rather than each ViewModel
/// reaching into Program.Services individually and silently no-op'ing when a service is missing.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private ViewModelBase _currentViewModel;

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _currentViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
    }

    public void NavigateToDashboard() => CurrentViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();

    public void NavigateToTemplates() => CurrentViewModel = _serviceProvider.GetRequiredService<TemplateManagementViewModel>();

    public void NavigateToBatch() => CurrentViewModel = _serviceProvider.GetRequiredService<BatchExecutionViewModel>();

    public void NavigateToSettings() => CurrentViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
}
