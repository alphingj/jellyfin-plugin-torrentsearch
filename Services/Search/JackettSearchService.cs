using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Jellyfin.Plugin.TorrentSearch.Models;
using Jellyfin.Plugin.TorrentSearch.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Polly;

namespace Jellyfin.Plugin.TorrentSearch.Services.Search;

public class JackettSearchService : ITorrentSearchService
{
    private PluginConfiguration _config => Plugin.Instance.Configuration;
    private readonly ILogger<JackettSearchService> _logger;
    private readonly HttpClient _httpClient;
    private readonly QualityParser _qualityParser;
    private readonly MemoryCache _releaseLinks = new(new MemoryCacheOptions { SizeLimit = 2000 });
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private static readonly XNamespace TorznabNs = "http://torznab.com/schemas/2015/feed";

    public JackettSearchService(ILogger<JackettSearchService> logger, HttpClient httpClient, QualityParser qualityParser)
    {
        _logger = logger;
        _httpClient = httpClient;
        _qualityParser = qualityParser;

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for Jackett search after {Timespan}", retryCount, timespan);
                });

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, PluginConfiguration config, CancellationToken ct)
    {
        var baseUrl = config.UseProwlarr ? config.ProwlarrUrl : config.JackettUrl;
        var apiKey = config.UseProwlarr ? config.ProwlarrApiKey : config.JackettApiKey;
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path));
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return await _httpClient.SendAsync(request, ct);
    }

    public async Task<SearchResult> SearchMoviesAsync(string query, int year = 0, CancellationToken ct = default)
    {
        var request = new TorznabSearchRequest
        {
            Query = query,
            MediaType = MediaType.Movie,
            Year = year > 0 ? year : null,
            MaxResults = _config.MaxSearchResults,
            MinSeeders = _config.MinSeeders
        };
        return await SearchAsync(request, ct);
    }

    public async Task<SearchResult> SearchShowsAsync(string query, int season = 0, int episode = 0, CancellationToken ct = default)
    {
        var request = new TorznabSearchRequest
        {
            Query = query,
            MediaType = MediaType.Series,
            Season = season > 0 ? season : null,
            Episode = episode > 0 ? episode : null,
            MaxResults = _config.MaxSearchResults,
            MinSeeders = _config.MinSeeders
        };
        return await SearchAsync(request, ct);
    }

    public async Task<SearchResult> SearchAsync(TorznabSearchRequest request, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<TorrentResult>();

        try
        {
            var config = _config;
            if (config.UseProwlarr)
            {
                var standard = request.MediaType == MediaType.Movie ? config.MovieSearchCategory : config.SeriesSearchCategory;
                var categoryIds = (request.CategoryIds ?? []).Where(id => id > 0).Distinct().ToArray();
                var useStandard = categoryIds.Length == 0 && request.CategoryId is null;
                var excluded = (useStandard ? standard?.ExcludedCategoryIds : request.ExcludedCategoryIds) ?? [];
                if (useStandard)
                    categoryIds = (standard?.CategoryIds ?? []).Where(id => id > 0).Distinct().ToArray();
                if (categoryIds.Length == 0)
                    categoryIds = [request.CategoryId ?? (request.MediaType == MediaType.Movie ? 2000 : 5000)];
                var categories = string.Join("&", categoryIds.Select(id => $"categories={id}"));
                var suffix = request.Season is int season
                    ? $" S{season:D2}" + (request.Episode is int episode ? $"E{episode:D2}" : "")
                    : request.Year is int year ? $" {year}" : "";
                var query = Uri.EscapeDataString(request.Query + suffix);
                var categoryFilter = request.AllCategories ? string.Empty : $"&{categories}";
                using var prowlarrResponse = await GetAsync($"api/v1/search?type=search&query={query}{categoryFilter}", config, ct);
                prowlarrResponse.EnsureSuccessStatusCode();
                var releases = await prowlarrResponse.Content.ReadFromJsonAsync<ProwlarrRelease[]>(cancellationToken: ct) ?? [];
                releases = releases.Where(release => !(release.Categories ?? []).Any(category => excluded.Contains(category.Id))).ToArray();
                foreach (var release in releases.Where(r => r.DownloadUrl != null))
                {
                    _releaseLinks.Set((release.IndexerId, release.Guid), release.DownloadUrl!,
                        new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });
                }
                results = FilterAndRankResults(releases
                    .Where(r => r.IndexerId > 0 && !string.IsNullOrEmpty(r.Guid))
                    .Select(r => new TorrentResult
                    {
                        Guid = r.Guid, IndexerId = r.IndexerId, Title = r.Title,
                        Size = r.Size, Seeders = r.Seeders ?? 0, Leechers = r.Leechers ?? 0,
                        Indexer = r.Indexer, PublishDate = r.PublishDate,
                        MediaType = request.MediaType, Quality = _qualityParser.ParseQuality(r.Title)
                    }).ToList(), request);
                return new SearchResult { Results = results, TotalResults = results.Count,
                    SearchQuery = request.Query, SearchTime = stopwatch.Elapsed };
            }

            var searchType = request.MediaType == MediaType.Movie ? "movie" : "tvsearch";
            var category = request.MediaType == MediaType.Movie ? "2000" : "5000";

            var queryParams = new Dictionary<string, string>
            {
                ["t"] = searchType,
                ["q"] = request.Query,
                ["cat"] = category,
                ["limit"] = request.MaxResults.ToString()
            };

            if (request.Year.HasValue)
                queryParams["year"] = request.Year.Value.ToString();
            if (request.Season.HasValue)
                queryParams["season"] = request.Season.Value.ToString();
            if (request.Episode.HasValue)
                queryParams["ep"] = request.Episode.Value.ToString();
            if (!string.IsNullOrEmpty(request.ImdbId))
                queryParams["imdbid"] = request.ImdbId;
            if (!string.IsNullOrEmpty(request.TmdbId))
                queryParams["tmdbid"] = request.TmdbId;

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"api/v2.0/indexers/all/results/torznab/api?{queryString}";

            _logger.LogDebug("Searching Jackett: {Url}", url);

            using var response = await _retryPolicy.ExecuteAsync(token => GetAsync(url, config, token), ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Jackett search failed: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                return new SearchResult { SearchQuery = request.Query, SearchTime = stopwatch.Elapsed };
            }

            var xmlContent = await response.Content.ReadAsStringAsync(ct);
            results = ParseTorznabXml(xmlContent, request.MediaType);

            results = FilterAndRankResults(results, request);
        }
        catch (Exception ex)
        {
            _logger.LogError("Torrent search failed: {ErrorType}", ex.GetType().Name);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }

        return new SearchResult
        {
            Results = results,
            TotalResults = results.Count,
            SearchQuery = request.Query,
            SearchTime = stopwatch.Elapsed
        };
    }

    private List<TorrentResult> ParseTorznabXml(string xmlContent, MediaType mediaType)
    {
        var results = new List<TorrentResult>();

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var channel = doc.Root?.Element("channel");
            if (channel == null) return results;

            var items = channel.Elements("item");
            foreach (var item in items)
            {
                try
                {
                    var title = item.Element("title")?.Value ?? string.Empty;
                    var link = item.Element("link")?.Value ?? string.Empty;
                    var guid = item.Element("guid")?.Value ?? string.Empty;
                    var pubDateStr = item.Element("pubDate")?.Value ?? string.Empty;
                    var sizeStr = item.Element("size")?.Value ?? "0";
                    var seedersStr = item.Elements(TorznabNs + "attr")
                        .FirstOrDefault(a => a.Attribute("name")?.Value == "seeders")?.Attribute("value")?.Value ?? "0";
                    var leechersStr = item.Elements(TorznabNs + "attr")
                        .FirstOrDefault(a => a.Attribute("name")?.Value == "peers")?.Attribute("value")?.Value ?? "0";
                    var indexer = item.Elements(TorznabNs + "attr")
                        .FirstOrDefault(a => a.Attribute("name")?.Value == "indexer")?.Attribute("value")?.Value ?? "Unknown";

                    var magnetLink = ExtractMagnetLink(item);

                    if (string.IsNullOrEmpty(magnetLink) && !string.IsNullOrEmpty(link) && link.StartsWith("magnet:"))
                    {
                        magnetLink = link;
                    }

                    if (string.IsNullOrEmpty(magnetLink))
                        continue;

                    var infoHash = ExtractInfoHash(magnetLink);
                    var quality = _qualityParser.ParseQuality(title);
                    var (season, episode) = MediaNamingHelper.ParseSeasonEpisode(title);
                    var year = MediaNamingHelper.ParseYear(title);

                    var result = new TorrentResult
                    {
                        Title = title,
                        MagnetLink = magnetLink,
                        TorrentUrl = link,
                        Size = long.TryParse(sizeStr, out var size) ? size : 0,
                        Seeders = int.TryParse(seedersStr, out var s) ? s : 0,
                        Leechers = int.TryParse(leechersStr, out var l) ? l : 0,
                        Indexer = indexer,
                        Category = mediaType == MediaType.Movie ? "Movies" : "TV",
                        PublishDate = DateTime.TryParse(pubDateStr, out var pubDate) ? pubDate : DateTime.MinValue,
                        InfoHash = infoHash,
                        Guid = guid,
                        Quality = quality,
                        MediaType = mediaType,
                        Season = season,
                        Episode = episode,
                        Year = year
                    };

                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing torrent item");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Torznab XML");
        }

        return results;
    }

    private string ExtractMagnetLink(XElement item)
    {
        var links = item.Elements("link");
        foreach (var link in links)
        {
            var value = link.Value;
            if (value.StartsWith("magnet:"))
                return value;
        }

        var enclosure = item.Element("enclosure");
        if (enclosure != null)
        {
            var url = enclosure.Attribute("url")?.Value;
            if (!string.IsNullOrEmpty(url) && url.StartsWith("magnet:"))
                return url;
        }

        return string.Empty;
    }

    private string ExtractInfoHash(string magnetLink)
    {
        var match = Regex.Match(magnetLink, @"xt=urn:btih:([a-zA-Z0-9]{32,40})");
        return match.Success ? match.Groups[1].Value.ToUpper() : string.Empty;
    }

    private List<TorrentResult> FilterAndRankResults(List<TorrentResult> results, TorznabSearchRequest request)
    {
        return results
            .Where(r => r.Seeders >= request.MinSeeders)
            .Where(r => !_config.ExcludedKeywords.Any(kw => r.Title.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            .Select(r =>
            {
                r.Score = CalculateScore(r);
                return r;
            })
            .OrderByDescending(r => string.IsNullOrWhiteSpace(request.Query) ? r.PublishDate.Ticks : (double)r.Score)
            .ThenByDescending(r => r.Seeders)
            .Take(request.MaxResults)
            .ToList();
    }

    private double CalculateScore(TorrentResult result)
    {
        double score = 0;

        score += result.Seeders * 10;
        score += result.Quality.QualityScore;

        if (_config.PreferredKeywords.Any(kw => result.Title.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            score += 50;

        if (_config.PreferredTrackers.Any(t => result.Indexer.Contains(t, StringComparison.OrdinalIgnoreCase)))
            score += 100;

        if (result.Size > 0 && result.Size < 500_000_000)
            score -= (double)result.Size / 1_000_000_000 * 10;

        return score;
    }

    private sealed record ProwlarrRelease(string Guid, int IndexerId, string Title,
        string Indexer, long Size, int? Seeders, int? Leechers, DateTime PublishDate, string? DownloadUrl, SearchCategory[]? Categories);

    public async Task<IReadOnlyList<SearchCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        var config = _config;
        if (!config.UseProwlarr) return [];
        using var response = await GetAsync("api/v1/indexer", config, ct);
        response.EnsureSuccessStatusCode();
        var indexers = await response.Content.ReadFromJsonAsync<ProwlarrIndexer[]>(cancellationToken: ct) ?? [];
        return indexers.Where(i => i.Enable).SelectMany(i => i.Capabilities?.Categories ?? [])
            .SelectMany(c => new[] { c }.Concat(c.SubCategories ?? []))
            .DistinctBy(c => c.Id).OrderBy(c => c.Id).ToArray();
    }

    public async Task<HttpResponseMessage?> GetReleaseAsync(int indexerId, string guid, CancellationToken ct)
    {
        var config = _config;
        if (!_releaseLinks.TryGetValue((indexerId, guid), out string? link) ||
            !Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var baseUri = new Uri(config.ProwlarrUrl.TrimEnd('/') + "/");
        if (!baseUri.IsBaseOf(uri))
        {
            // Prowlarr may advertise its public address in a proxy download link.
            var proxyPath = $"/{indexerId}/download";
            if (!uri.AbsolutePath.EndsWith(proxyPath, StringComparison.Ordinal)) return null;
            uri = new Uri(baseUri, proxyPath.TrimStart('/') + uri.Query);
        }

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-Api-Key", config.ProwlarrApiKey);
        return await client.SendAsync(request, ct);
    }

    private sealed record ProwlarrIndexer(bool Enable, ProwlarrCapabilities? Capabilities);
    private sealed record ProwlarrCapabilities(SearchCategory[] Categories);

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var config = _config;
            var url = config.UseProwlarr ? "api/v1/system/status" : "api/v2.0/indexers/all/results/torznab/api?t=search&q=test&limit=1";
            using var response = await GetAsync(url, config, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
