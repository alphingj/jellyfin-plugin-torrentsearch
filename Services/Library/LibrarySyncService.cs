using Jellyfin.Plugin.TorrentSearch.Models;
using Jellyfin.Plugin.TorrentSearch.Helpers;
using Jellyfin.Plugin.TorrentSearch.Services.Metadata;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.Services.Library;

public class LibrarySyncService : ILibrarySyncService
{
    private readonly PluginConfiguration _config;
    private readonly IMetadataService _metadataService;
    private readonly ILogger<LibrarySyncService> _logger;

    public LibrarySyncService(
        PluginConfiguration config,
        IMetadataService metadataService,
        ILogger<LibrarySyncService> logger)
    {
        _config = config;
        _metadataService = metadataService;
        _logger = logger;
    }

    public async Task<SyncResult> SyncCompletedDownloadAsync(string downloadHash, string downloadPath, MediaType mediaType, CancellationToken ct = default)
    {
        try
        {
            var files = Directory.GetFiles(downloadPath, "*.*", SearchOption.AllDirectories)
                .Where(f => IsVideoFile(f))
                .ToList();

            if (!files.Any())
            {
                return new SyncResult { Success = false, ErrorMessage = "No video files found in download" };
            }

            var mainFile = files.OrderByDescending(f => new FileInfo(f).Length).First();
            var fileName = Path.GetFileNameWithoutExtension(mainFile);
            var ext = Path.GetExtension(mainFile);

            var (title, year, season, episode) = MediaNamingHelper.ParseMediaInfo(fileName);
            var quality = MediaNamingHelper.ParseQuality(fileName);

            MovieMetadata? movieMeta = null;
            SeriesMetadata? seriesMeta = null;
            EpisodeMetadata? episodeMeta = null;

            if (mediaType == MediaType.Movie)
            {
                var searchResults = await _metadataService.SearchMoviesAsync(title, year, ct);
                movieMeta = searchResults.FirstOrDefault(m => 
                    m.Year == year || Math.Abs(m.Year - year) <= 1);
                
                if (movieMeta == null)
                {
                    return new SyncResult { Success = false, ErrorMessage = "Could not find movie metadata" };
                }
            }
            else if (mediaType == MediaType.Series)
            {
                var searchResults = await _metadataService.SearchSeriesAsync(title, ct);
                seriesMeta = searchResults.FirstOrDefault();
                
                if (seriesMeta == null)
                {
                    return new SyncResult { Success = false, ErrorMessage = "Could not find series metadata" };
                }

                if (season.HasValue && episode.HasValue)
                {
                    var seasonMeta = await _metadataService.GetSeasonAsync(seriesMeta.TmdbId, season.Value, ct);
                    episodeMeta = seasonMeta?.Episodes.FirstOrDefault(e => e.EpisodeNumber == episode.Value);
                }
            }

            var targetPath = GetTargetPath(mediaType, title, year, season, episode, movieMeta, seriesMeta, episodeMeta, quality, ext);
            var targetDir = Path.GetDirectoryName(targetPath)!;
            
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            if (File.Exists(targetPath))
            {
                return new SyncResult { Success = false, ErrorMessage = "File already exists in library" };
            }

            File.Move(mainFile, targetPath);

            var createdFiles = new List<string> { targetPath };

            if (mediaType == MediaType.Movie && movieMeta != null)
            {
                var nfoPath = Path.ChangeExtension(targetPath, ".nfo");
                var nfoContent = NfoGenerator.GenerateMovieNfo(movieMeta);
                await File.WriteAllTextAsync(nfoPath, nfoContent, ct);
                createdFiles.Add(nfoPath);

                if (!string.IsNullOrEmpty(movieMeta.PosterPath))
                {
                    var posterUrl = $"https://image.tmdb.org/t/p/w500{movieMeta.PosterPath}";
                    var posterPath = Path.Combine(targetDir, "poster.jpg");
                    await DownloadImageAsync(posterUrl, posterPath, ct);
                    createdFiles.Add(posterPath);
                }
            }
            else if (mediaType == MediaType.Series && seriesMeta != null)
            {
                var nfoPath = Path.Combine(targetDir, "tvshow.nfo");
                var nfoContent = NfoGenerator.GenerateSeriesNfo(seriesMeta);
                await File.WriteAllTextAsync(nfoPath, nfoContent, ct);
                createdFiles.Add(nfoPath);

if (episodeMeta != null)
                {
                    var episodeNfoPath = Path.ChangeExtension(targetPath, ".nfo");
                    var episodeNfoContent = NfoGenerator.GenerateEpisodeNfo(episodeMeta, season ?? 0, episode ?? 0);
                    await File.WriteAllTextAsync(episodeNfoPath, episodeNfoContent, ct);
                    createdFiles.Add(episodeNfoPath);
                }

                if (!string.IsNullOrEmpty(movieMeta.PosterPath))
                {
                    var posterUrl = $"https://image.tmdb.org/t/p/w500{movieMeta.PosterPath}";
                    var posterPath = Path.Combine(targetDir, "poster.jpg");
                    await DownloadImageAsync(posterUrl, posterPath, ct);
                    createdFiles.Add(posterPath);
                }
            }

            await RefreshLibraryAsync(GetLibraryId(mediaType), ct);

            return new SyncResult
            {
                Success = true,
                ItemName = Path.GetFileName(targetPath),
                CreatedFiles = createdFiles
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing download {Hash}", downloadHash);
            return new SyncResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncResult> ScanAndImportAsync(string libraryPath, MediaType mediaType, CancellationToken ct = default)
    {
        if (!Directory.Exists(libraryPath))
        {
            return new SyncResult { Success = false, ErrorMessage = "Library path does not exist" };
        }

        try
        {
            var files = Directory.GetFiles(libraryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => IsVideoFile(f))
                .ToList();

            var imported = 0;
            foreach (var file in files)
            {
                var nfoPath = Path.ChangeExtension(file, ".nfo");
                if (!File.Exists(nfoPath))
                {
                    imported++;
                }
            }

            await RefreshLibraryAsync(GetLibraryId(mediaType), ct);

            return new SyncResult 
            { 
                Success = true, 
                ItemName = $"Scanned {files.Count} files, {imported} new",
                CreatedFiles = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning library {Path}", libraryPath);
            return new SyncResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<bool> RefreshLibraryAsync(string libraryId, CancellationToken ct = default)
    {
        try
        {
            var plugin = Plugin.Instance;
            if (plugin == null) return false;
            
            var apiUrl = $"{plugin.Configuration.ServerUrl}/Library/Media/Updated";
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = new StringContent($"{{\"Updates\":[{{\"Path\":\"{libraryId}\",\"ItemId\":\"{libraryId}\"}}]}}", 
                    System.Text.Encoding.UTF8, "application/json")
            };

            request.Headers.Add("X-Emby-Token", plugin.Configuration.ApiKey);
            
            using var client = new HttpClient();
            var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string GetTargetPath(MediaType mediaType, string title, int year, int? season, int? episode, 
        MovieMetadata? movieMeta, SeriesMetadata? seriesMeta, EpisodeMetadata? episodeMeta, QualityInfo quality, string ext)
    {
        var basePath = mediaType == MediaType.Movie ? _config.MoviesLibraryPath : _config.TvShowsLibraryPath;
        var cleanTitle = MediaNamingHelper.SanitizeFileName(title);
        var cleanSeries = seriesMeta != null ? MediaNamingHelper.SanitizeFileName(seriesMeta.Name) : cleanTitle;

        if (mediaType == MediaType.Movie && movieMeta != null)
        {
            var folderName = $"{cleanTitle} ({year}) [imdbid-{movieMeta.ImdbId}]";
            if (!string.IsNullOrEmpty(quality.ReleaseGroup))
                folderName += $" [{quality.ReleaseGroup}]";

            var fileName = $"{cleanTitle} ({year}) [imdbid-{movieMeta.ImdbId}]";
            if (!string.IsNullOrEmpty(quality.Badge))
                fileName += $" [{quality.Badge}]";

            return Path.Combine(basePath, folderName, fileName + ext);
        }
        else if (mediaType == MediaType.Series && seriesMeta != null && season.HasValue)
        {
            var seasonDir = Path.Combine(basePath, cleanSeries, $"Season {season.Value:D2}");
            var episodeTitle = episodeMeta?.Name ?? $"Episode {episode.Value:D2}";
            var fileName = $"{cleanSeries} - S{season.Value:D2}E{episode.Value:D2} - {MediaNamingHelper.SanitizeFileName(episodeTitle)}";
            return Path.Combine(seasonDir, fileName + ext);
        }

        return Path.Combine(basePath, cleanTitle + ext);
    }

    private string GetLibraryId(MediaType mediaType) => mediaType == MediaType.Movie ? "Movies" : "TV Shows";

    private bool IsVideoFile(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        return ext is ".mkv" or ".mp4" or ".avi" or ".mov" or ".m4v" or ".ts" or ".wmv" or ".flv";
    }

    private async Task DownloadImageAsync(string url, string path, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(path, data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download image {Url}", url);
        }
    }
}