using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using EmailAutomation.Application.Services;
using EmailAutomation.Infrastructure.Database;
using EmailAutomation.Infrastructure.Excel;
using EmailAutomation.Infrastructure.Templates;
using EmailAutomation.Infrastructure.Email;
using Serilog;

namespace EmailAutomation.UI;

internal class Program
{
    public static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/emailautomation.log", rollingInterval: RollingInterval.Day)
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
        var dbPath = "email_automation.db";
        var connStr = $"Data Source={dbPath}";

        var dbInit = new DatabaseInitializer(connStr);
        dbInit.Initialize();

        services.AddSingleton<IRepository>(new SqliteRepository(connStr));
        services.AddSingleton<IExcelReader, ClosedXmlExcelReader>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();

        // Use placeholders for credentials. They should be configured via UI/Settings in the future.
        services.AddSingleton<IEmailSender>(new GmailSender("credentials.json", "token.json"));

        services.AddTransient<BatchExecutionService>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
