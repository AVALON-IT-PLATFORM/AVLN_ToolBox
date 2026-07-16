using Avln.ToolBox.Services;
using Avln.ToolBox.ViewModels;
using Avln.ToolBox.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Avln.ToolBox.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolBox(this IServiceCollection services)
    {
        var paths = new AppPaths();
        services.AddSingleton(paths);

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(paths.LogDirectory));
        });

        services.AddSingleton(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AVLN-ToolBox/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        });

        services.AddSingleton<SettingsService>();
        services.AddSingleton<InstalledStateService>();
        services.AddSingleton<RevitDetectionService>();
        services.AddSingleton<ProcessDetectionService>();
        services.AddSingleton<FileSystemService>();
        services.AddSingleton<GitHubReleaseService>();
        services.AddSingleton<PackageInstallService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();

        return services;
    }
}
