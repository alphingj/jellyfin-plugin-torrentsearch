using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TorrentSearch.Models;

public class TorrentResult
{
    public string Title { get; set; } = string.Empty;
    public string MagnetLink { get; set; } = string.Empty;
    public string TorrentUrl { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Seeders { get; set; }
    public int Leechers { get; set; }
    public string Indexer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public string InfoHash { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public QualityInfo Quality { get; set; } = new();
    public double Score { get; set; }
    public MediaType MediaType { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public int Year { get; set; }
    public string ImdbId { get; set; } = string.Empty;
    public string TmdbId { get; set; } = string.Empty;
}

public class QualityInfo
{
    public string Resolution { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsHDR { get; set; }
    public string Audio { get; set; } = string.Empty;
    public string ReleaseGroup { get; set; } = string.Empty;
    public int QualityScore { get; set; }
    public string Badge => GetQualityBadge();

    private string GetQualityBadge()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Resolution)) parts.Add(Resolution);
        if (!string.IsNullOrEmpty(Source)) parts.Add(Source);
        else if (!string.IsNullOrEmpty(Codec)) parts.Add(Codec);
        if (IsHDR) parts.Add("HDR");
        return string.Join(" · ", parts);
    }
}

public enum MediaType
{
    Movie,
    Series,
    Episode,
    Anime,
    Unknown
}

public class SearchResult
{
    public List<TorrentResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public TimeSpan SearchTime { get; set; }
}

public class DownloadStatus
{
    public string Hash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Progress { get; set; }
    public long Downloaded { get; set; }
    public long TotalSize { get; set; }
    public double DownloadSpeed { get; set; }
    public double UploadSpeed { get; set; }
    public int Seeders { get; set; }
    public int Peers { get; set; }
    public string State { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public DateTime AddedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public string Category { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
}

public class DownloadResult
{
    public bool Success { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class SyncResult
{
    public bool Success { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> CreatedFiles { get; set; } = new();
}

public class TorznabSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public int? Year { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string ImdbId { get; set; } = string.Empty;
    public string TmdbId { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 20;
    public int MinSeeders { get; set; } = 1;
}

public class DownloadOptions
{
    public string MagnetLink { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public string Category { get; set; } = "movies";
    public bool AutoStart { get; set; } = true;
    public bool SequentialDownload { get; set; } = false;
    public bool FirstLastPiecePriority { get; set; } = false;
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

public class HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public string Component { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Latency { get; set; }
}