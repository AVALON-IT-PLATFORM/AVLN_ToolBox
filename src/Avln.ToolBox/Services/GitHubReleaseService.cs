using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avln.ToolBox.Models;
using Microsoft.Extensions.Logging;

namespace Avln.ToolBox.Services;

public sealed class GitHubReleaseService
{
    private static readonly Uri ReleasesUri = new(
        "https://api.github.com/repos/AVALON-IT-PLATFORM/AVLN_ToolBox/releases?per_page=20");

    private static readonly IReadOnlyDictionary<int, string> AssetNames = new Dictionary<int, string>
    {
        [2021] = "Avalon.External.R21.zip",
        [2022] = "Avalon.External.R22.zip",
        [2023] = "Avalon.External.R23.zip",
        [2024] = "Avalon.External.R24.zip",
        [2025] = "Avalon.External.R25.zip"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReleaseService> _logger;

    public GitHubReleaseService(HttpClient httpClient, ILogger<GitHubReleaseService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<int, ReleasePackage>> GetLatestPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ReleasesUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new Dictionary<int, ReleasePackage>();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GitHub Releases вернул статус {StatusCode}. RateLimitRemaining={RateLimitRemaining}",
                    (int)response.StatusCode,
                    response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
                        ? values.FirstOrDefault()
                        : "unknown");
                return new Dictionary<int, ReleasePackage>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubReleaseDto>>(
                stream,
                JsonOptions,
                cancellationToken) ?? [];

            var packages = new Dictionary<int, ReleasePackage>();
            foreach (var release in releases.Where(item => !item.Draft && !item.Prerelease))
            {
                foreach (var (year, expectedAssetName) in AssetNames)
                {
                    if (packages.ContainsKey(year))
                    {
                        continue;
                    }

                    var asset = release.Assets.FirstOrDefault(item =>
                        string.Equals(item.Name, expectedAssetName, StringComparison.OrdinalIgnoreCase));
                    if (asset is null || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri))
                    {
                        continue;
                    }

                    packages[year] = new ReleasePackage(
                        year,
                        NormalizeVersion(release.TagName),
                        asset.Name,
                        downloadUri);
                }
            }

            return packages;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Превышен timeout запроса GitHub Releases");
            return new Dictionary<int, ReleasePackage>();
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Не удалось получить GitHub Releases");
            return new Dictionary<int, ReleasePackage>();
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "GitHub Releases вернул некорректный JSON");
            return new Dictionary<int, ReleasePackage>();
        }
    }

    private static string NormalizeVersion(string tagName)
    {
        var normalized = (tagName ?? string.Empty).Trim().TrimStart('v', 'V');
        return string.IsNullOrWhiteSpace(normalized) ? "0.0.0" : normalized;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto> Assets { get; init; } = [];
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;
    }
}
