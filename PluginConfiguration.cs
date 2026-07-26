using MediaBrowser.Model.Plugins;
using System.Collections.Generic;

namespace Jellyfin.Plugin.TorrentSearch;

public class PluginConfiguration : BasePluginConfiguration
{
    public string JackettUrl { get; set; } = "http://localhost:9117";
    public string JackettApiKey { get; set; } = string.Empty;
    public bool UseProwlarr { get; set; } = false;
    public string ProwlarrUrl { get; set; } = "http://localhost:9696";
    public string ProwlarrApiKey { get; set; } = string.Empty;

    public TorrentClientType TorrentClient { get; set; } = TorrentClientType.QBittorrent;
    public string QBittorrentUrl { get; set; } = "http://localhost:8080";
    public string QBittorrentUsername { get; set; } = "admin";
    public string QBittorrentPassword { get; set; } = string.Empty;

    public string DownloadPath { get; set; } = "/downloads";
    public int MaxConcurrentDownloads { get; set; } = 3;

    public string TmdbApiKey { get; set; } = string.Empty;
    public int MaxSearchResults { get; set; } = 20;
    public int MinSeeders { get; set; } = 1;
    public List<string> PreferredKeywords { get; set; } = new() { "1080p", "x264", "x265", "hevc" };
    public List<string> ExcludedKeywords { get; set; } = new() { "cam", "ts", "tc", "scr", "dvdscr", "r5", "tc" };
    public List<string> PreferredTrackers { get; set; } = new();

    public string MoviesLibraryPath { get; set; } = "/media/movies";
    public string TvShowsLibraryPath { get; set; } = "/media/tv";
    public bool AutoRefreshLibrary { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 5;

    public string ServerUrl { get; set; } = "http://localhost:8096";
    public string ApiKey { get; set; } = string.Empty;

    public string RealDebridApiKey { get; set; } = string.Empty;
    public string AllDebridApiKey { get; set; } = string.Empty;
    public string PremiumizeApiKey { get; set; } = string.Empty;
}

public enum TorrentClientType
{
    QBittorrent,
    MonoTorrent,
    Transmission,
    Deluge
}