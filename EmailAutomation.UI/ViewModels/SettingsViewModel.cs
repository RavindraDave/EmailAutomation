using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IEmailConnectionTester _connectionTester;

    public string Title => "Settings";

    public ObservableCollection<string> Providers { get; } = new() { "SMTP", "GmailAPI" };

    private string _provider = "SMTP";
    public string Provider
    {
        get => _provider;
        set
        {
            if (SetProperty(ref _provider, value))
            {
                OnPropertyChanged(nameof(IsSmtp));
                OnPropertyChanged(nameof(IsGmailApi));
            }
        }
    }

    public bool IsSmtp => Provider.Equals("SMTP", StringComparison.OrdinalIgnoreCase);
    public bool IsGmailApi => Provider.Equals("GmailAPI", StringComparison.OrdinalIgnoreCase);

    private string _smtpHost = "smtp.gmail.com";
    public string SmtpHost
    {
        get => _smtpHost;
        set => SetProperty(ref _smtpHost, value);
    }

    private int _smtpPort = 587;
    public int SmtpPort
    {
        get => _smtpPort;
        set => SetProperty(ref _smtpPort, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _fromDisplayName = string.Empty;
    public string FromDisplayName
    {
        get => _fromDisplayName;
        set => SetProperty(ref _fromDisplayName, value);
    }

    /// <summary>
    /// Plaintext password, held only in memory while the Settings screen is open. Never populated
    /// from storage - the user must retype it to change it, and it is cleared immediately after Save.
    /// </summary>
    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _credentialsPath = "credentials.json";
    public string CredentialsPath
    {
        get => _credentialsPath;
        set => SetProperty(ref _credentialsPath, value);
    }

    private string _tokenPath = "token.json";
    public string TokenPath
    {
        get => _tokenPath;
        set => SetProperty(ref _tokenPath, value);
    }

    private int _delayBetweenSendsMs = 1000;
    public int DelayBetweenSendsMs
    {
        get => _delayBetweenSendsMs;
        set => SetProperty(ref _delayBetweenSendsMs, value);
    }

    private int _dailySendCap = 450;
    public int DailySendCap
    {
        get => _dailySendCap;
        set => SetProperty(ref _dailySendCap, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Null = no test run yet (neutral), true = last test succeeded, false = last test failed.</summary>
    private bool? _lastTestSucceeded;
    public bool? LastTestSucceeded
    {
        get => _lastTestSucceeded;
        set => SetProperty(ref _lastTestSucceeded, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand TestConnectionCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IEmailConnectionTester connectionTester)
    {
        _settingsService = settingsService;
        _connectionTester = connectionTester;

        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
        TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => !IsBusy);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsService.LoadAsync();
        Provider = settings.Provider;
        SmtpHost = settings.SmtpHost;
        SmtpPort = settings.SmtpPort;
        Username = settings.Username;
        FromDisplayName = settings.FromDisplayName;
        CredentialsPath = settings.CredentialsPath;
        TokenPath = settings.TokenPath;
        DelayBetweenSendsMs = settings.DelayBetweenSendsMs;
        DailySendCap = settings.DailySendCap;

        StatusMessage = settings.HasCredentials
            ? "Saved settings loaded. Enter a new password only if you want to change it."
            : "No settings saved yet - fill in your email provider details below.";
    }

    private AppSettings BuildSettingsFromFields(AppSettings existing)
    {
        existing.Provider = Provider;
        existing.SmtpHost = SmtpHost;
        existing.SmtpPort = SmtpPort;
        existing.Username = Username;
        existing.FromDisplayName = FromDisplayName;
        existing.CredentialsPath = CredentialsPath;
        existing.TokenPath = TokenPath;
        existing.DelayBetweenSendsMs = DelayBetweenSendsMs;
        existing.DailySendCap = DailySendCap;
        return existing;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var existing = await _settingsService.LoadAsync();
            var settings = BuildSettingsFromFields(existing);

            if (!string.IsNullOrEmpty(Password))
            {
                _settingsService.SetPassword(settings, Password);
                Password = string.Empty;
            }

            await _settingsService.SaveAsync(settings);
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Testing connection...";
        LastTestSucceeded = null;
        try
        {
            var existing = await _settingsService.LoadAsync();
            var settings = BuildSettingsFromFields(existing);

            var passwordToTest = string.IsNullOrEmpty(Password)
                ? _settingsService.GetPassword(existing) ?? string.Empty
                : Password;

            var (success, message) = await _connectionTester.TestAsync(settings, passwordToTest);
            LastTestSucceeded = success;
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            LastTestSucceeded = false;
            StatusMessage = $"Connection test failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
