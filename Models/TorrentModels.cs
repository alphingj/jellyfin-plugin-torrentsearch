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

// TMDB Metadata Models
public class MovieMetadata
{
    public int TmdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string BackdropPath { get; set; } = string.Empty;
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public List<string> Genres { get; set; } = new();
    public string ImdbId { get; set; } = string.Empty;
    public int Runtime { get; set; }
    public string ReleaseDate { get; set; } = string.Empty;
    public string Certification { get; set; } = string.Empty;
    public List<ProductionCompany> ProductionCompanies { get; set; } = new();
    public List<ProductionCountry> Countries { get; set; } = new();
    public List<SpokenLanguage> SpokenLanguages { get; set; } = new();
    public Credits Credits { get; set; } = new();
    public string TrailerUrl { get; set; } = string.Empty;
}

public class SeriesMetadata
{
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public DateTime? FirstAirDate { get; set; }
    public string Overview { get; set; } = string.Empty;
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public List<string> Genres { get; set; } = new();
    public string ImdbId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ContentRating { get; set; } = string.Empty;
    public List<Network> Networks { get; set; } = new();
    public List<string> OriginCountry { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public Credits Credits { get; set; } = new();
    public List<SeasonMetadata> Seasons { get; set; } = new();
}

public class SeasonMetadata
{
    public int SeasonNumber { get; set; }
    public int EpisodeCount { get; set; }
    public DateTime? AirDate { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public List<EpisodeMetadata> Episodes { get; set; } = new();
}

public class EpisodeMetadata
{
    public int EpisodeNumber { get; set; }
    public int SeasonNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public DateTime? AirDate { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public int TmdbId { get; set; }
    public string Director { get; set; } = string.Empty;
    public string Writer { get; set; } = string.Empty;
    public List<GuestStar> GuestStars { get; set; } = new();
    public string SeriesName { get; set; } = string.Empty;
    public string StillPath { get; set; } = string.Empty;
}

public class GuestStar
{
    public string Name { get; set; } = string.Empty;
    public string Character { get; set; } = string.Empty;
    public string ProfilePath { get; set; } = string.Empty;
}

public class ProductionCompany
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
}

public class ProductionCountry
{
    public string Iso3166_1 { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SpokenLanguage
{
    public string Iso639_1 { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Network
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
}

public class Credits
{
    public List<CastMember> Cast { get; set; } = new();
    public List<CrewMember> Crew { get; set; } = new();
}

public class CastMember
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Character { get; set; } = string.Empty;
    public int Order { get; set; }
    public string ProfilePath { get; set; } = string.Empty;
}

public class CrewMember
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ProfilePath { get; set; } = string.Empty;
}