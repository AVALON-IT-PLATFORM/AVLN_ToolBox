using Avln.ToolBox.Infrastructure;
using Avln.ToolBox.Models;
using Microsoft.Win32;

namespace Avln.ToolBox.Services;

public sealed class RevitDetectionService
{
    private const int FirstSupportedYear = 2021;
    private const int LastSupportedYear = 2025;
    private readonly AppPaths _paths;

    public RevitDetectionService(AppPaths paths)
    {
        _paths = paths;
    }

    public Task<IReadOnlyList<RevitInstallation>> DetectAsync(
        ToolBoxSettings settings,
        InstalledPackageState installedState,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<RevitInstallation>>(() =>
        {
            var detected = new Dictionary<int, string?>();

            for (var year = FirstSupportedYear; year <= LastSupportedYear; year++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var installPath = TryGetRegistryInstallPath(year)
                    ?? TryGetProgramFilesInstallPath(year);

                var configuredAddinsPath = Path.Combine(settings.InstallBasePath, year.ToString());
                var defaultAddinsPath = Path.Combine(_paths.DefaultInstallBasePath, year.ToString());
                if (installPath is not null ||
                    Directory.Exists(configuredAddinsPath) ||
                    Directory.Exists(defaultAddinsPath))
                {
                    detected[year] = installPath;
                }
            }

            foreach (var yearText in installedState.Packages.Keys)
            {
                if (int.TryParse(yearText, out var year) &&
                    year is >= FirstSupportedYear and <= LastSupportedYear)
                {
                    detected.TryAdd(year, TryGetRegistryInstallPath(year) ?? TryGetProgramFilesInstallPath(year));
                }
            }

            return detected
                .OrderBy(pair => pair.Key)
                .Select(pair => new RevitInstallation(pair.Key, pair.Value))
                .ToArray();
        }, cancellationToken);
    }

    private static string? TryGetRegistryInstallPath(int year)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var subKeyPaths = new[]
        {
            $@"SOFTWARE\Autodesk\Revit\Autodesk Revit {year}",
            $@"SOFTWARE\Autodesk\Revit\{year}"
        };

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                foreach (var subKeyPath in subKeyPaths)
                {
                    using var key = baseKey.OpenSubKey(subKeyPath);
                    var installLocation = key?.GetValue("InstallationLocation") as string
                        ?? key?.GetValue("InstallLocation") as string;

                    if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                    {
                        return installLocation;
                    }
                }
            }
            catch
            {
                // Registry is only the primary detection source; folder fallback follows.
            }
        }

        return null;
    }

    private static string? TryGetProgramFilesInstallPath(int year)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var path = Path.Combine(programFiles, "Autodesk", $"Revit {year}");
        return Directory.Exists(path) ? path : null;
    }
}
