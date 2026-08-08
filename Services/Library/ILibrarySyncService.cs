using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Services.Library;

public interface ILibrarySyncService
{
    Task<SyncResult> SyncCompletedDownloadAsync(string downloadHash, string downloadPath, MediaType mediaType, int? season = null, int? episode = null, CancellationToken ct = default);
    Task<SyncResult> ScanAndImportAsync(string libraryPath, MediaType mediaType, CancellationToken ct = default);
    Task<bool> RefreshLibraryAsync(string libraryId, CancellationToken ct = default);
    Task<List<string>> GetLibraryPathsAsync(MediaType mediaType, CancellationToken ct = default);
}