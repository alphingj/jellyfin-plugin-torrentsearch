using Jellyfin.Plugin.TorrentSearch.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.Services.Metadata;

public class TmdbMetadataService : IMetadataService
{
    private readonly ILogger<TmdbMetadataService> _logger;
    private readonly string _defaultApiKey = "f3b5f5f5f5f5f5f5f5f5f5f5f5f5f5f5";

    public TmdbMetadataService(PluginConfiguration config, ILogger<TmdbMetadataService> logger)
    {
        _logger = logger;
    }

    public Task<MovieMetadata?> GetMovieAsync(int tmdbId, CancellationToken ct = default)
    {
        return Task.FromResult<MovieMetadata?>(null);
    }

    public Task<MovieMetadata?> GetMovieByImdbIdAsync(string imdbId, CancellationToken ct = default)
    {
        return Task.FromResult<MovieMetadata?>(null);
    }

    public Task<List<MovieMetadata>> SearchMoviesAsync(string query, int year = 0, CancellationToken ct = default)
    {
        return Task.FromResult(new List<MovieMetadata>());
    }

    public Task<SeriesMetadata?> GetSeriesAsync(int tmdbId, CancellationToken ct = default)
    {
        return Task.FromResult<SeriesMetadata?>(null);
    }

    public Task<SeriesMetadata?> GetSeriesByImdbIdAsync(string imdbId, CancellationToken ct = default)
    {
        return Task.FromResult<SeriesMetadata?>(null);
    }

    public Task<List<SeriesMetadata>> SearchSeriesAsync(string query, CancellationToken ct = default)
    {
        return Task.FromResult(new List<SeriesMetadata>());
    }

    public Task<SeasonMetadata?> GetSeasonAsync(int seriesId, int seasonNumber, CancellationToken ct = default)
    {
        return Task.FromResult<SeasonMetadata?>(null);
    }

    public Task<EpisodeMetadata?> GetEpisodeAsync(int seriesId, int seasonNumber, int episodeNumber, CancellationToken ct = default)
    {
        return Task.FromResult<EpisodeMetadata?>(null);
    }
}