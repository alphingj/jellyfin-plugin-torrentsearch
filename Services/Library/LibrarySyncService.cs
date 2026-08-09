using Jellyfin.Plugin.TorrentSearch.Models;
using Jellyfin.Plugin.TorrentSearch.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.Services.Library;

public class LibrarySyncService : ILibrarySyncService
{
    private readonly PluginConfiguration _config;
    private readonly ILogger<LibrarySyncService> _logger;

    public LibrarySyncService(
        PluginConfiguration config,
        ILogger<LibrarySyncService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<SyncResult> SyncCompletedDownloadAsync(string downloadHash, string downloadPath, MediaType mediaType, int? season = null, int? episode = null, CancellationToken ct = default)
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

            var (title, year, parsedSeason, parsedEpisode) = MediaNamingHelper.ParseMediaInfo(fileName);
            season ??= parsedSeason;
            episode ??= parsedEpisode;
            var quality = MediaNamingHelper.ParseQuality(fileName);

            if (string.IsNullOrWhiteSpace(title))
            {
                return new SyncResult { Success = false, ErrorMessage = "Could not parse a title from the file name" };
            }

            var targetPath = GetTargetPath(mediaType, title, year, season, episode, quality, ext);
            var targetDir = Path.GetDirectoryName(targetPath)!;

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            if (File.Exists(targetPath))
            {
                return new SyncResult { Success = false, ErrorMessage = "File already exists in library" };
            }

            File.Move(mainFile, targetPath);

            var createdFiles = new List<string> { targetPath };

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

            var imported = files.Count;

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

    public Task<List<string>> GetLibraryPathsAsync(MediaType mediaType, CancellationToken ct = default)
    {
        var path = mediaType == MediaType.Movie ? _config.MoviesLibraryPath : _config.TvShowsLibraryPath;
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(new List<string>());

        return Task.FromResult(Directory.Exists(path)
            ? Directory.GetDirectories(path).ToList()
            : new List<string>());
    }

    private string GetTargetPath(MediaType mediaType, string title, int year, int? season, int? episode, QualityInfo quality, string ext)
    {
        var basePath = mediaType == MediaType.Movie ? _config.MoviesLibraryPath : _config.TvShowsLibraryPath;
        var cleanTitle = MediaNamingHelper.SanitizeFileName(title);

        if (mediaType == MediaType.Movie)
        {
            var folderName = MediaNamingHelper.BuildMovieFolderName(cleanTitle, year, null, quality.ReleaseGroup);
            var fileName = MediaNamingHelper.BuildMovieFileName(cleanTitle, year, null, quality.ReleaseGroup, ext);

            return Path.Combine(basePath, folderName, fileName);
        }

        if (season.HasValue && episode.HasValue)
        {
            var seasonDir = Path.Combine(basePath, cleanTitle, $"Season {season.Value:D2}");
            var fileName = MediaNamingHelper.BuildEpisodeFileName(cleanTitle, season.Value, episode.Value, null, quality.ReleaseGroup, ext);
            return Path.Combine(seasonDir, fileName);
        }

        return Path.Combine(basePath, cleanTitle + ext);
    }

    private string GetLibraryId(MediaType mediaType) => mediaType == MediaType.Movie ? "Movies" : "TV Shows";

    private bool IsVideoFile(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        return ext is ".mkv" or ".mp4" or ".avi" or ".mov" or ".m4v" or ".ts" or ".wmv" or ".flv";
    }
}
