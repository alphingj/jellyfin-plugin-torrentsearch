using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Services.Search;

public interface ITorrentSearchService
{
    Task<SearchResult> SearchMoviesAsync(string query, int year = 0, CancellationToken ct = default);
    Task<SearchResult> SearchShowsAsync(string query, int season = 0, int episode = 0, CancellationToken ct = default);
    Task<SearchResult> SearchAsync(TorznabSearchRequest request, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}