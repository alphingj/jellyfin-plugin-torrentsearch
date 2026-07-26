using Jellyfin.Plugin.TorrentSearch.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.Services.Download;

public class QBittorrentDownloadService : IDownloadManagerService
{
    private readonly ILogger<QBittorrentDownloadService> _logger;
    private readonly PluginConfiguration _config;

    public QBittorrentDownloadService(PluginConfiguration config, ILogger<QBittorrentDownloadService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<DownloadResult> AddMagnetAsync(DownloadOptions options, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding magnet: {Magnet}", options.MagnetLink);
        return Task.FromResult(new DownloadResult { Success = true, Hash = Guid.NewGuid().ToString("N") });
    }

    public Task<DownloadResult> AddTorrentFileAsync(byte[] torrentData, DownloadOptions options, CancellationToken ct = default)
    {
        return Task.FromResult(new DownloadResult { Success = true, Hash = Guid.NewGuid().ToString("N") });
    }

    public Task<List<DownloadStatus>> GetActiveDownloadsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<DownloadStatus>());
    }

    public Task<DownloadStatus?> GetDownloadStatusAsync(string hash, CancellationToken ct = default)
    {
        return Task.FromResult<DownloadStatus?>(null);
    }

    public Task<bool> PauseDownloadAsync(string hash, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ResumeDownloadAsync(string hash, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> RemoveDownloadAsync(string hash, bool deleteFiles = false, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<Dictionary<string, object>> GetClientInfoAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["type"] = "qBittorrent",
            ["status"] = "stub"
        });
    }
}