using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Services.Metadata;

public interface IMetadataService
{
    Task<MovieMetadata?> GetMovieAsync(int tmdbId, CancellationToken ct = default);
    Task<MovieMetadata?> GetMovieByImdbIdAsync(string imdbId, CancellationToken ct = default);
    Task<List<MovieMetadata>> SearchMoviesAsync(string query, int year = 0, CancellationToken ct = default);
    Task<SeriesMetadata?> GetSeriesAsync(int tmdbId, CancellationToken ct = default);
    Task<SeriesMetadata?> GetSeriesByImdbIdAsync(string imdbId, CancellationToken ct = default);
    Task<List<SeriesMetadata>> SearchSeriesAsync(string query, CancellationToken ct = default);
    Task<SeasonMetadata?> GetSeasonAsync(int seriesId, int seasonNumber, CancellationToken ct = default);
    Task<EpisodeMetadata?> GetEpisodeAsync(int seriesId, int seasonNumber, int episodeNumber, CancellationToken ct = default);
}