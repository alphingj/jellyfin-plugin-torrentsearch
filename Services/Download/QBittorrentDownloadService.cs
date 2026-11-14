using QBittorrent.Client;
using Jellyfin.Plugin.TorrentSearch.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jellyfin.Plugin.TorrentSearch.Services.Download;

public class QBittorrentDownloadService : IDownloadManagerService, IDisposable
{
    private readonly QBittorrentClient _client;
    private readonly ILogger<QBittorrentDownloadService> _logger;
    private readonly PluginConfiguration _config;
    private readonly bool _usesApiKey;
    private readonly HttpClient _categoryClient;
    private bool _loggedIn = false;

    public QBittorrentDownloadService(PluginConfiguration config, ILogger<QBittorrentDownloadService> logger)
    {
        _config = config;
        _logger = logger;
        var uri = new Uri(config.QBittorrentUrl);
        _usesApiKey = !string.IsNullOrWhiteSpace(config.QBittorrentApiKey);
        HttpMessageHandler handler;
        if (_usesApiKey)
        {
            handler = new ApiKeyHandler(config.QBittorrentApiKey.Trim());
        }
        else
        {
            handler = new HttpClientHandler();
        }
        _client = new QBittorrentClient(uri, handler, true);
        _categoryClient = new HttpClient(handler, false) { BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/") };
    }

    private async Task EnsureLoginAsync(CancellationToken ct = default)
    {
        if (!_usesApiKey && !_loggedIn)
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
            await _client.GetQBittorrentVersionAsync(ct);
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
            return (await GetDownloadsAsync(ct))
                .Where(t => t.State != nameof(TorrentState.StalledUpload)
                    && t.State != nameof(TorrentState.Uploading)
                    && t.State != nameof(TorrentState.PausedUpload))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active downloads from qBittorrent");
            return new List<DownloadStatus>();
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        var categories = await _categoryClient.GetFromJsonAsync<Dictionary<string, JsonElement>>("api/v2/torrents/categories", ct);
        return categories?.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
    }

    public async Task<List<DownloadStatus>> GetDownloadsAsync(CancellationToken ct = default)
    {
        await EnsureLoginAsync(ct);
        var torrents = await _client.GetTorrentListAsync(new TorrentListQuery(), ct);
        return torrents.Select(MapTorrentToStatus).ToList();
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
            if (_usesApiKey) await UpdateTorrentAsync("stop", hash, ct);
            else await _client.PauseAsync(hash, ct);
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
            if (_usesApiKey) await UpdateTorrentAsync("start", hash, ct);
            else await _client.ResumeAsync(hash, ct);
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
            if (_usesApiKey) await UpdateTorrentAsync("delete", hash, ct, deleteFiles);
            else await _client.DeleteAsync(new[] { hash }, deleteFiles, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing download {Hash}", hash);
            return false;
        }
    }

    private async Task UpdateTorrentAsync(string action, string hash, CancellationToken ct, bool? deleteFiles = null)
    {
        var fields = new Dictionary<string, string> { ["hashes"] = hash };
        if (deleteFiles.HasValue) fields["deleteFiles"] = deleteFiles.Value ? "true" : "false";
        using var content = new FormUrlEncodedContent(fields);
        using var response = await _categoryClient.PostAsync($"api/v2/torrents/{action}", content, ct);
        response.EnsureSuccessStatusCode();
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

    public void Dispose()
    {
        _client.Dispose();
        _categoryClient.Dispose();
    }

    private sealed class ApiKeyHandler(string apiKey)
        : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // API keys require Web API v2. The client probes this legacy route to select its API version.
            var uri = request.RequestUri!;
            if (request.Method == HttpMethod.Get && uri.AbsolutePath.EndsWith("/version/api", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("25")
                });
            }

            var path = uri.AbsolutePath switch
            {
                "/version/qbittorrent" => "/api/v2/app/version",
                "/query/torrents" => "/api/v2/torrents/info",
                "/command/upload" or "/command/download" => "/api/v2/torrents/add",
                "/command/pause" or "/api/v2/torrents/pause" => "/api/v2/torrents/stop",
                "/command/resume" or "/api/v2/torrents/resume" => "/api/v2/torrents/start",
                "/command/delete" or "/command/deletePerm" => "/api/v2/torrents/delete",
                _ => uri.AbsolutePath
            };
            if (path != uri.AbsolutePath)
            {
                request.RequestUri = new UriBuilder(uri) { Path = path }.Uri;
            }

            if (uri.AbsolutePath is "/command/delete" or "/command/deletePerm")
                return DeleteLegacyAsync(request, uri.AbsolutePath == "/command/deletePerm", cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            if (path == "/api/v2/torrents/info")
                return ReadCompatibleTorrentListAsync(request, cancellationToken);
            return base.SendAsync(request, cancellationToken);
        }

        private async Task<HttpResponseMessage> ReadCompatibleTorrentListAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return response;

            try
            {
                var torrents = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken: ct);
                if (torrents is null) return response;
                foreach (var torrent in torrents)
                {
                    switch (torrent?["state"]?.GetValue<string>())
                    {
                        case "stoppedDL": torrent["state"] = "pausedDL"; break;
                        case "stoppedUP": torrent["state"] = "pausedUP"; break;
                    }
                }

                response.Content.Dispose();
                response.Content = JsonContent.Create(torrents);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        private async Task<HttpResponseMessage> DeleteLegacyAsync(HttpRequestMessage request, bool deleteFiles, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            request.Content.Dispose();
            request.Content = new StringContent(body + "&deleteFiles=" + (deleteFiles ? "true" : "false"),
                System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return await base.SendAsync(request, ct);
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
