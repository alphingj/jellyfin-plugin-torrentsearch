using Jellyfin.Plugin.TorrentSearch.Services.Download;
using Jellyfin.Plugin.TorrentSearch.Services.Search;
using Jellyfin.Plugin.TorrentSearch.Models;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Jellyfin.Plugin.TorrentSearch.Controllers;

[ApiController]
[Authorize]
[Route("TorrentSearch/UserDownloads")]
public sealed class UserDownloadsController(IDownloadManagerService downloads, ITorrentSearchService search, IUserManager users, ILogger<UserDownloadsController> logger, IMemoryCache cache, IAuthorizationService authorization) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst("Jellyfin-UserId")?.Value, out var id) ? id : Guid.Empty;
    private async Task<bool> IsAdmin() => (await authorization.AuthorizeAsync(User,
        MediaBrowser.Common.Api.Policies.RequiresElevation)).Succeeded;
    private static readonly SemaphoreSlim StartLock = new(1);
    private static string OwnerPath(string hash) => Path.Combine(
        Path.GetDirectoryName(Plugin.Instance.ConfigurationFilePath)!, "torrentsearch-owners", hash.ToLowerInvariant() + ".json");
    private sealed record Owner(Guid UserId, DateTime AddedOn);

    private static Guid FindOwner(DownloadStatus item)
    {
        if (!IsTorrentHash(item.Hash) || item.AddedOn == default) return Guid.Empty;
        try
        {
            var owner = JsonSerializer.Deserialize<Owner>(System.IO.File.ReadAllText(OwnerPath(item.Hash)));
            return owner?.AddedOn == item.AddedOn ? owner.UserId : Guid.Empty;
        }
        catch (IOException) { return Guid.Empty; }
        catch (JsonException) { return Guid.Empty; }
    }
    [AllowAnonymous]
    [HttpGet("Page")]
    public IActionResult Page() =>
        File(typeof(UserDownloadsController).Assembly.GetManifestResourceStream(
            "Jellyfin.Plugin.TorrentSearch.Configuration.downloads.html")!, "text/html; charset=utf-8");

    [HttpGet("Status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var config = Plugin.Instance.Configuration;
        if (!config.UseProwlarr) return Ok(new { status = "ok" });

        var status = await cache.GetOrCreateAsync(("TorrentSearchHealth", config.ProwlarrUrl), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                client.DefaultRequestHeaders.Add("X-Api-Key", config.ProwlarrApiKey);
                var url = config.ProwlarrUrl.TrimEnd('/');
                using var health = await client.GetFromJsonAsync<JsonDocument>(url + "/api/v1/health", ct);
                using var indexers = await client.GetFromJsonAsync<JsonDocument>(url + "/api/v1/indexerstatus", ct);
                var unhealthy = health?.RootElement.EnumerateArray().Any(item =>
                    item.TryGetProperty("source", out var source) &&
                    source.GetString()?.Contains("Indexer", StringComparison.OrdinalIgnoreCase) == true) == true;
                var disabled = indexers?.RootElement.EnumerateArray().Any(item =>
                    item.TryGetProperty("disabledTill", out var until) && until.ValueKind == JsonValueKind.String &&
                    until.TryGetDateTimeOffset(out var date) && date > DateTimeOffset.UtcNow) == true;
                return unhealthy || disabled ? "indexer" : "ok";
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or JsonException or OperationCanceledException)
            {
                return "unavailable";
            }
        });
        return Ok(new { status });
    }

    [HttpGet("Search")]
    public async Task<IActionResult> Search(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            return BadRequest();

        var result = await search.SearchAsync(new TorznabSearchRequest { Query = query.Trim(), AllCategories = true }, ct);
        return Ok(result.Results.Select(item => new { title = item.Title, size = item.Size, seeders = item.Seeders, indexer = item.Indexer, guid = item.Guid, indexerId = item.IndexerId }));
    }

    [HttpPost("Start")]
    public async Task<IActionResult> Start(ReleaseSelection selection, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("Jellyfin-UserId")?.Value, out var userId) || userId == Guid.Empty)
            return Forbid();

        var config = Plugin.Instance.Configuration;
        if (!config.UseProwlarr || selection.IndexerId <= 0 || string.IsNullOrWhiteSpace(selection.Guid))
            return BadRequest();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Post, config.ProwlarrUrl.TrimEnd('/') + "/api/v1/search");
        request.Headers.Add("X-Api-Key", config.ProwlarrApiKey);
        await StartLock.WaitAsync(ct);
        try
        {
            string? hash = null;
            try
            {
                using var release = await search.GetReleaseAsync(selection.IndexerId, selection.Guid, ct);
                if (release?.Headers.Location is { Scheme: "magnet" } magnet)
                    hash = MonoTorrent.MagnetLink.Parse(magnet.AbsoluteUri).InfoHash.ToHex();
                else if (release?.IsSuccessStatusCode == true)
                    hash = MonoTorrent.Torrent.Load(await release.Content.ReadAsByteArrayAsync(ct)).InfoHash.ToHex();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Could not read torrent identity: {ErrorType}", ex.GetType().Name);
            }

            hash = hash?.ToLowerInvariant();
            if (hash is null)
                logger.LogWarning("No torrent hash available for ownership assignment (indexer {IndexerId})", selection.IndexerId);
            var canAssign = hash is not null && IsTorrentHash(hash) &&
                !(await downloads.GetDownloadsAsync(ct)).Any(item => string.Equals(item.Hash, hash, StringComparison.OrdinalIgnoreCase));
            request.Content = JsonContent.Create(new { guid = selection.Guid, indexerId = selection.IndexerId });
            using var response = await client.SendAsync(request, ct);
            if (canAssign && response.IsSuccessStatusCode)
            {
                try
                {
                    await RecordOwner(hash!, userId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Could not record torrent ownership: {ErrorType}", ex.GetType().Name);
                }
            }
            return Ok(new { success = response.IsSuccessStatusCode, status = (int)response.StatusCode });
        }
        finally
        {
            StartLock.Release();
        }
    }

    private async Task RecordOwner(string hash, Guid userId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var ct = timeout.Token;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var torrent = await downloads.GetDownloadStatusAsync(hash, ct);
            if (torrent is not null && torrent.AddedOn != default)
            {
                if (FindOwner(torrent) != Guid.Empty) return;
                var path = OwnerPath(hash);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temp = path + ".tmp";
                await System.IO.File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new Owner(userId, torrent.AddedOn)), ct);
                System.IO.File.Move(temp, path, true);
                return;
            }
            await Task.Delay(500, ct);
        }
    }

    public sealed record ReleaseSelection(string Guid, int IndexerId);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await downloads.GetDownloadsAsync(ct);
        var admin = await IsAdmin();
        return Ok(items.Where(item => item.Progress < 1).Select(item =>
        {
            var owner = FindOwner(item);
            return new {
            hash = item.Hash,
            name = item.Name,
            progress = item.Progress,
            downloadSpeed = item.DownloadSpeed,
            state = item.State,
            owner = owner == Guid.Empty ? "System" : users.GetUserById(owner)?.Username ?? "System",
            canManage = admin || owner != Guid.Empty && owner == UserId
            };
        }));
    }

    [HttpPost("{hash}/Pause")]
    public async Task<IActionResult> Pause(string hash, CancellationToken ct) =>
        await CanManage(hash, ct) ? Ok(await downloads.PauseDownloadAsync(hash, ct)) : Forbid();

    [HttpPost("{hash}/Resume")]
    public async Task<IActionResult> Resume(string hash, CancellationToken ct) =>
        await CanManage(hash, ct) ? Ok(await downloads.ResumeDownloadAsync(hash, ct)) : Forbid();

    [HttpDelete("{hash}")]
    public async Task<IActionResult> Remove(string hash, CancellationToken ct) =>
        await CanManage(hash, ct) ? Ok(await downloads.RemoveDownloadAsync(hash, false, ct)) : Forbid();

    private async Task<bool> CanManage(string hash, CancellationToken ct) =>
        IsTorrentHash(hash) && (await IsAdmin() || UserId != Guid.Empty &&
            await downloads.GetDownloadStatusAsync(hash, ct) is { } item && FindOwner(item) == UserId);

    private static bool IsTorrentHash(string hash) =>
        hash.Length is 40 or 64 && hash.All(Uri.IsHexDigit);
}
