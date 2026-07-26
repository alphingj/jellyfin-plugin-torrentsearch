using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Services.Download;

public interface IDownloadManagerService
{
    Task<DownloadResult> AddMagnetAsync(DownloadOptions options, CancellationToken ct = default);
    Task<DownloadResult> AddTorrentFileAsync(byte[] torrentData, DownloadOptions options, CancellationToken ct = default);
    Task<List<DownloadStatus>> GetActiveDownloadsAsync(CancellationToken ct = default);
    Task<DownloadStatus?> GetDownloadStatusAsync(string hash, CancellationToken ct = default);
    Task<bool> PauseDownloadAsync(string hash, CancellationToken ct = default);
    Task<bool> ResumeDownloadAsync(string hash, CancellationToken ct = default);
    Task<bool> RemoveDownloadAsync(string hash, bool deleteFiles = false, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<Dictionary<string, object>> GetClientInfoAsync(CancellationToken ct = default);
}