using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Services.Library;

public interface ILibrarySyncService
{
    Task<SyncResult> SyncCompletedDownloadAsync(string downloadHash, string downloadPath, MediaType mediaType, CancellationToken ct = default);
    Task<SyncResult> ScanAndImportAsync(string libraryPath, MediaType mediaType, CancellationToken ct = default);
    Task<bool> RefreshLibraryAsync(string libraryId, CancellationToken ct = default);
}