using System.Collections.ObjectModel;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.UI.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    public string Title => "Dashboard";

    public ObservableCollection<string> RecentLogs { get; } = new ObservableCollection<string>();

    public int TotalSuccess { get; set; } = 0;
    public int TotalFailures { get; set; } = 0;

    public DashboardViewModel()
    {
        // Sample data
        RecentLogs.Add("System started.");
    }
}
