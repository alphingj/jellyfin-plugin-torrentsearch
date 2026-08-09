using Jellyfin.Plugin.TorrentSearch.Models;
using Jellyfin.Plugin.TorrentSearch.Services.Download;
using Jellyfin.Plugin.TorrentSearch.Services.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.BackgroundServices;

public class DownloadMonitorService : BackgroundService
{
    private readonly IDownloadManagerService _downloadService;
    private readonly ILibrarySyncService _librarySync;
    private readonly PluginConfiguration _config;
    private readonly ILogger<DownloadMonitorService> _logger;
    private readonly HashSet<string> _completedHashes = new();

    public DownloadMonitorService(
        IDownloadManagerService downloadService,
        ILibrarySyncService librarySync,
        PluginConfiguration config,
        ILogger<DownloadMonitorService> logger)
    {
        _downloadService = downloadService;
        _librarySync = librarySync;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Download monitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var downloads = await _downloadService.GetActiveDownloadsAsync(stoppingToken);

                foreach (var download in downloads)
                {
                    if (download.Progress >= 1.0 && !_completedHashes.Contains(download.Hash))
                    {
                        _completedHashes.Add(download.Hash);

                        var mediaType = download.MediaType;
                        if (mediaType == MediaType.Unknown)
                        {
                            mediaType = download.Category?.Contains("tv", StringComparison.OrdinalIgnoreCase) == true
                                ? MediaType.Series
                                : MediaType.Movie;
                        }

                        await _librarySync.SyncCompletedDownloadAsync(
                            download.Hash,
                            download.SavePath,
                            mediaType,
                            download.Season,
                            download.Episode,
                            stoppingToken);
                    }
                }

                _completedHashes.RemoveWhere(h => !downloads.Any(d => d.Hash == h));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in download monitor");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}

public class LibraryScannerService : BackgroundService
{
    private readonly ILibrarySyncService _librarySync;
    private readonly PluginConfiguration _config;
    private readonly ILogger<LibraryScannerService> _logger;

    public LibraryScannerService(
        ILibrarySyncService librarySync,
        PluginConfiguration config,
        ILogger<LibraryScannerService> logger)
    {
        _librarySync = librarySync;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.AutoRefreshLibrary)
        {
            _logger.LogInformation("Auto library refresh disabled");
            return;
        }

        _logger.LogInformation("Library scanner started with interval: {Interval} minutes", _config.ScanIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _librarySync.ScanAndImportAsync(_config.MoviesLibraryPath, MediaType.Movie, stoppingToken);
                await _librarySync.ScanAndImportAsync(_config.TvShowsLibraryPath, MediaType.Series, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in library scanner");
            }

            await Task.Delay(TimeSpan.FromMinutes(_config.ScanIntervalMinutes), stoppingToken);
        }
    }
}

public class HealthCheckService : BackgroundService
{
    private readonly IDownloadManagerService _downloadService;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(IDownloadManagerService downloadService, ILogger<HealthCheckService> logger)
    {
        _downloadService = downloadService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var healthy = await _downloadService.TestConnectionAsync(stoppingToken);
                _logger.LogDebug("Health check: {Status}", healthy ? "Healthy" : "Unhealthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}