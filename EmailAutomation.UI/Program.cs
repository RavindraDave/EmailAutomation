using System;
using System.IO;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using EmailAutomation.Application.Services;
using EmailAutomation.Infrastructure;
using EmailAutomation.Infrastructure.Database;
using EmailAutomation.Infrastructure.Email;
using EmailAutomation.Infrastructure.Excel;
using EmailAutomation.Infrastructure.Security;
using EmailAutomation.Infrastructure.Settings;
using EmailAutomation.Infrastructure.Templates;
using EmailAutomation.UI.Services;
using EmailAutomation.UI.ViewModels;
using Serilog;

namespace EmailAutomation.UI;

internal class Program
{
    public static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "emailautomation.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var connStr = $"Data Source={AppPaths.DatabasePath}";

        var dbInit = new DatabaseInitializer(connStr);
        dbInit.Initialize();

        services.AddSingleton<IRepository>(new SqliteRepository(connStr));
        services.AddSingleton<IExcelReader, ClosedXmlExcelReader>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();

        // All provider/credential configuration now lives in per-user settings (Settings screen),
        // not in appsettings.json, so the email sender is built on demand from AppSettings.
        services.AddSingleton<ISecretProtector>(SecretProtectorFactory.Create());
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IEmailSenderFactory, EmailSenderFactory>();
        services.AddSingleton<IEmailConnectionTester, EmailConnectionTester>();
        services.AddSingleton<IEmailAddressValidator, MimeKitEmailAddressValidator>();
        services.AddSingleton<ISampleTemplateGenerator, ClosedXmlSampleTemplateGenerator>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton(TimeProvider.System);

        services.AddTransient<BatchExecutionService>();
        services.AddTransient<BatchValidationService>();

        // ViewModels are transient and resolved by MainWindowViewModel on navigation, so each
        // visit gets fresh data (e.g. the Dashboard's counts) without a separate refresh step.
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TemplateManagementViewModel>();
        services.AddTransient<BatchExecutionViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindowViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
