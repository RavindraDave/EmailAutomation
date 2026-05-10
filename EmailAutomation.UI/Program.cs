using System;
using System.IO;
using Avalonia;
using Microsoft.Extensions.Configuration;
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
    public static IConfiguration? Configuration { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/emailautomation.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            var services = new ServiceCollection();
            ConfigureServices(services, Configuration);
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

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var dbPath = "email_automation.db";
        var connStr = $"Data Source={dbPath}";

        var dbInit = new DatabaseInitializer(connStr);
        dbInit.Initialize();

        services.AddSingleton<IRepository>(new SqliteRepository(connStr));
        services.AddSingleton<IExcelReader, ClosedXmlExcelReader>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();

        var provider = config["EmailProvider"] ?? "SMTP";

        if (provider.Equals("SMTP", StringComparison.OrdinalIgnoreCase))
        {
            var host = config["SMTP:Host"] ?? "smtp.gmail.com";
            var portStr = config["SMTP:Port"] ?? "587";
            int port = int.TryParse(portStr, out var p) ? p : 587;
            var username = config["SMTP:Username"] ?? "";
            var password = config["SMTP:Password"] ?? "";

            services.AddSingleton<IEmailSender>(new SmtpEmailSender(host, port, username, password));
        }
        else
        {
            var credPath = config["GmailAPI:CredentialsPath"] ?? "credentials.json";
            var tokenPath = config["GmailAPI:TokenPath"] ?? "token.json";

            services.AddSingleton<IEmailSender>(new GmailSender(credPath, tokenPath));
        }

        services.AddTransient<BatchExecutionService>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
