using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.TorrentSearch.Models;
using Jellyfin.Plugin.TorrentSearch.Services.Search;
using Jellyfin.Plugin.TorrentSearch.Services.Metadata;
using Jellyfin.Plugin.TorrentSearch.Services.Download;
using Jellyfin.Plugin.TorrentSearch.Services.Library;

namespace Jellyfin.Plugin.TorrentSearch.Controllers;

[ApiController]
[Route("TorrentSearch")]
[Authorize(Policy = "RequiresAdmin")]
public class TorrentSearchController : ControllerBase
{
    private readonly ITorrentSearchService _searchService;
    private readonly IMetadataService _metadataService;
    private readonly IDownloadManagerService _downloadService;
    private readonly ILibrarySyncService _librarySync;
    private readonly PluginConfiguration _config;

    public TorrentSearchController(
        ITorrentSearchService searchService,
        IMetadataService metadataService,
        IDownloadManagerService downloadService,
        ILibrarySyncService librarySync)
    {
        _searchService = searchService;
        _metadataService = metadataService;
        _downloadService = downloadService;
        _librarySync = librarySync;
        _config = Plugin.Instance.Configuration;
    }

    [HttpGet("Config")]
    public PluginConfiguration GetConfig()
    {
        return Plugin.Instance.Configuration;
    }

    [HttpPost("Config")]
    public ActionResult SaveConfig([FromBody] PluginConfiguration config)
    {
        if (config == null)
        {
            return BadRequest();
        }

        Plugin.Instance.UpdateConfiguration(config);
        return NoContent();
    }

    [HttpGet("Search/Movie")]
    public async Task<SearchResult> SearchMovies(string query, int year = 0, CancellationToken ct = default)
    {
        return await _searchService.SearchMoviesAsync(query, year, ct);
    }

    [HttpGet("Search/Show")]
    public async Task<SearchResult> SearchShows(string query, int season = 0, int episode = 0, CancellationToken ct = default)
    {
        return await _searchService.SearchShowsAsync(query, season, episode, ct);
    }

    [HttpPost("Search")]
    public async Task<SearchResult> Search([FromBody] TorznabSearchRequest request, CancellationToken ct = default)
    {
        return await _searchService.SearchAsync(request, ct);
    }

    [HttpGet("Search/Autocomplete")]
    public async Task<List<string>> SearchAutocomplete(string query, CancellationToken ct = default)
    {
        var movies = await _metadataService.SearchMoviesAsync(query, 0, ct);
        var shows = await _metadataService.SearchSeriesAsync(query, ct);
        
        return movies.Select(m => m.Title)
            .Concat(shows.Select(s => s.Name))
            .Distinct()
            .Take(10)
            .ToList();
    }

    [HttpPost("Download/Start")]
    public async Task<DownloadResult> StartDownload([FromBody] DownloadOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(options.MagnetLink))
        {
            return new DownloadResult { Success = false, ErrorMessage = "Magnet link is required" };
        }

        if (string.IsNullOrEmpty(options.SavePath))
        {
            options.SavePath = options.Category == "tv" 
                ? _config.TvShowsLibraryPath 
                : _config.MoviesLibraryPath;
        }

        return await _downloadService.AddMagnetAsync(options, ct);
    }

    [HttpPost("Download/AddTorrent")]
    public async Task<DownloadResult> AddTorrentFile([FromBody] TorrentFileRequest request, CancellationToken ct = default)
    {
        if (request.TorrentData == null || request.TorrentData.Length == 0)
        {
            return new DownloadResult { Success = false, ErrorMessage = "Torrent file data is required" };
        }

        return await _downloadService.AddTorrentFileAsync(request.TorrentData, request.Options, ct);
    }

    [HttpGet("Downloads")]
    public async Task<List<DownloadStatus>> GetActiveDownloads(CancellationToken ct = default)
    {
        return await _downloadService.GetActiveDownloadsAsync(ct);
    }

    [HttpGet("Downloads/{hash}")]
    public async Task<DownloadStatus?> GetDownloadStatus(string hash, CancellationToken ct = default)
    {
        return await _downloadService.GetDownloadStatusAsync(hash, ct);
    }

    [HttpPost("Downloads/{hash}/Pause")]
    public async Task<bool> PauseDownload(string hash, CancellationToken ct = default)
    {
        return await _downloadService.PauseDownloadAsync(hash, ct);
    }

    [HttpPost("Downloads/{hash}/Resume")]
    public async Task<bool> ResumeDownload(string hash, CancellationToken ct = default)
    {
        return await _downloadService.ResumeDownloadAsync(hash, ct);
    }

    [HttpDelete("Downloads/{hash}")]
    public async Task<bool> RemoveDownload(string hash, [FromQuery] bool deleteFiles = false, CancellationToken ct = default)
    {
        return await _downloadService.RemoveDownloadAsync(hash, deleteFiles, ct);
    }

    [HttpPost("Library/Sync")]
    public async Task<SyncResult> SyncLibrary([FromBody] LibrarySyncRequest request, CancellationToken ct = default)
    {
        var mediaType = request.MediaType ?? MediaType.Movie;
        var libraryPath = mediaType == MediaType.Movie 
            ? _config.MoviesLibraryPath 
            : _config.TvShowsLibraryPath;

        return await _librarySync.ScanAndImportAsync(libraryPath, mediaType, ct);
    }

    [HttpPost("Library/Refresh")]
    public async Task<bool> RefreshLibrary([FromBody] LibraryRefreshRequest request, CancellationToken ct = default)
    {
        return await _librarySync.RefreshLibraryAsync(request.LibraryId, ct);
    }

    [HttpGet("Config/TestIndexer")]
    public async Task<bool> TestIndexerConnection(CancellationToken ct = default)
    {
        return await _searchService.TestConnectionAsync(ct);
    }

    [HttpGet("Config/TestClient")]
    public async Task<bool> TestClientConnection(CancellationToken ct = default)
    {
        return await _downloadService.TestConnectionAsync(ct);
    }

    [HttpGet("Config/ClientInfo")]
    public async Task<Dictionary<string, object>> GetClientInfo(CancellationToken ct = default)
    {
        return await _downloadService.GetClientInfoAsync(ct);
    }
}

public class TorrentFileRequest
{
    public byte[] TorrentData { get; set; } = Array.Empty<byte>();
    public DownloadOptions Options { get; set; } = new();
}

public class LibrarySyncRequest
{
    public MediaType? MediaType { get; set; }
}

public class LibraryRefreshRequest
{
    public string LibraryId { get; set; } = string.Empty;
}