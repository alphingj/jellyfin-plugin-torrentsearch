using QBittorrent.Client;
using Jellyfin.Plugin.TorrentSearch.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.Services.Download;

public class QBittorrentDownloadService : IDownloadManagerService
{
    private readonly QBittorrentClient _client;
    private readonly ILogger<QBittorrentDownloadService> _logger;
    private readonly PluginConfiguration _config;
    private bool _loggedIn = false;

    public QBittorrentDownloadService(PluginConfiguration config, ILogger<QBittorrentDownloadService> logger)
    {
        _config = config;
        _logger = logger;
        _client = new QBittorrentClient(new Uri(config.QBittorrentUrl));
    }

    private async Task EnsureLoginAsync(CancellationToken ct = default)
    {
        if (!_loggedIn)
        {
            await _client.LoginAsync(_config.QBittorrentUsername, _config.QBittorrentPassword, ct);
            _loggedIn = true;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to qBittorrent");
            _loggedIn = false;
            return false;
        }
    }

    public async Task<DownloadResult> AddMagnetAsync(DownloadOptions options, CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);

            var request = new AddTorrentUrlsRequest(new[] { new Uri(options.MagnetLink) })
            {
                DownloadFolder = options.SavePath,
                Category = options.Category,
                Paused = !options.AutoStart,
                CreateRootFolder = true
            };

            await _client.AddTorrentsAsync(request, ct);

            // Get the hash by checking recently added torrents
            await Task.Delay(500, ct); // Small delay for qBittorrent to process
            var torrents = await _client.GetTorrentListAsync(new TorrentListQuery(), ct);
            var added = torrents.OrderByDescending(t => t.AddedOn).FirstOrDefault();

            return new DownloadResult { Success = true, Hash = added?.Hash ?? string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding magnet to qBittorrent");
            return new DownloadResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<DownloadResult> AddTorrentFileAsync(byte[] torrentData, DownloadOptions options, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"jpt_{Guid.NewGuid():N}.torrent");
        try
        {
            await EnsureLoginAsync(ct);

            await File.WriteAllBytesAsync(tempPath, torrentData, ct);
            var request = new AddTorrentFilesRequest(new[] { tempPath })
            {
                DownloadFolder = options.SavePath,
                Category = options.Category,
                Paused = !options.AutoStart,
                CreateRootFolder = true
            };

            await _client.AddTorrentsAsync(request, ct);

            await Task.Delay(500, ct);
            var torrents = await _client.GetTorrentListAsync(new TorrentListQuery(), ct);
            var added = torrents.OrderByDescending(t => t.AddedOn).FirstOrDefault();

            return new DownloadResult { Success = true, Hash = added?.Hash ?? string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding torrent file to qBittorrent");
            return new DownloadResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp torrent file {Path}", tempPath);
            }
        }
    }

    public async Task<List<DownloadStatus>> GetActiveDownloadsAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            var torrents = await _client.GetTorrentListAsync(new TorrentListQuery(), ct);

            return torrents
                .Where(t => t.State != TorrentState.StalledUpload
                    && t.State != TorrentState.Uploading
                    && t.State != TorrentState.PausedUpload)
                .Select(MapTorrentToStatus)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active downloads from qBittorrent");
            return new List<DownloadStatus>();
        }
    }

    public async Task<DownloadStatus?> GetDownloadStatusAsync(string hash, CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            var torrents = await _client.GetTorrentListAsync(new TorrentListQuery(), ct);
            var torrent = torrents.FirstOrDefault(t => t.Hash == hash);

            return torrent != null ? MapTorrentToStatus(torrent) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting download status for {Hash}", hash);
            return null;
        }
    }

    public async Task<bool> PauseDownloadAsync(string hash, CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            await _client.PauseAsync(hash, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing download {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> ResumeDownloadAsync(string hash, CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            await _client.ResumeAsync(hash, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming download {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> RemoveDownloadAsync(string hash, bool deleteFiles = false, CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            await _client.DeleteAsync(new[] { hash }, deleteFiles, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing download {Hash}", hash);
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetClientInfoAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureLoginAsync(ct);
            var version = await _client.GetQBittorrentVersionAsync(ct);

            return new Dictionary<string, object>
            {
                ["version"] = version,
                ["type"] = "qBittorrent"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client info");
            return new Dictionary<string, object> { ["error"] = ex.Message };
        }
    }

    private DownloadStatus MapTorrentToStatus(TorrentInfo torrent)
    {
        return new DownloadStatus
        {
            Hash = torrent.Hash ?? string.Empty,
            Name = torrent.Name ?? string.Empty,
            Progress = torrent.Progress,
            Downloaded = torrent.Downloaded ?? 0,
            TotalSize = torrent.Size,
            DownloadSpeed = torrent.DownloadSpeed,
            UploadSpeed = torrent.UploadSpeed,
            Seeders = torrent.TotalSeeds,
            Peers = torrent.TotalLeechers,
            State = torrent.State.ToString(),
            SavePath = torrent.SavePath ?? string.Empty,
            AddedOn = torrent.AddedOn.GetValueOrDefault(),
            CompletedOn = torrent.CompletionOn,
            Category = torrent.Category ?? string.Empty
        };
    }
}
