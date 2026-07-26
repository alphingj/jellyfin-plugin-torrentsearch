using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Jellyfin.Plugin.TorrentSearch.Models;
using Jellyfin.Plugin.TorrentSearch.Helpers;
using Microsoft.Extensions.Logging;
using Polly;

namespace Jellyfin.Plugin.TorrentSearch.Services.Search;

public class JackettSearchService : ITorrentSearchService
{
    private readonly PluginConfiguration _config;
    private readonly ILogger<JackettSearchService> _logger;
    private readonly HttpClient _httpClient;
    private readonly QualityParser _qualityParser;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private static readonly XNamespace TorznabNs = "http://torznab.com/schemas/2015/feed";

    public JackettSearchService(PluginConfiguration config, ILogger<JackettSearchService> logger, HttpClient httpClient, QualityParser qualityParser)
    {
        _config = config;
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

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _config.UseProwlarr ? _config.ProwlarrUrl : _config.JackettUrl;
        var apiKey = _config.UseProwlarr ? _config.ProwlarrApiKey : _config.JackettApiKey;
        
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(url, ct));
            
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
            _logger.LogError(ex, "Error searching Jackett for query: {Query}", request.Query);
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
            .OrderByDescending(r => r.Score)
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

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var url = "api/v2.0/indexers/all/results/torznab/api?t=search&q=test&limit=1";
            var response = await _httpClient.GetAsync(url, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}