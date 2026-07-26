using System.Text.RegularExpressions;
using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Helpers;

public static class MediaNamingHelper
{
    private static readonly Regex SeasonEpisodeRegex = new(
        @"[Ss](\d{1,2})[Ee](\d{1,2})",
        RegexOptions.Compiled);

    private static readonly Regex YearRegex = new(
        @"\b(19|20)\d{2}\b",
        RegexOptions.Compiled);

    private static readonly Regex ReleaseGroupRegex = new(
        @"[-_\.]([A-Za-z0-9]+)$",
        RegexOptions.Compiled);

    private static readonly Regex QualityRegex = new(
        @"\b(4320p|2160p|1080p|720p|576p|480p|8K|4K|UHD|HDR|REMUX|BLURAY|BDRip|BRRip|WEB-DL|WEBRip|HDTV|PDTV|DSR|SATRip|DVBRip|DVDRip|DVDR|VHSRip|TVRip|HDDVD)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CodecRegex = new(
        @"\b(x264|x265|h264|h265|HEVC|AVC|MPEG-2|VC-1|XviD|DivX)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static (string Title, int Year, int? Season, int? Episode) ParseMediaInfo(string fileName)
    {
        var cleanName = fileName;
        var year = 0;
        int? season = null;
        int? episode = null;

        var yearMatch = YearRegex.Match(cleanName);
        if (yearMatch.Success && int.TryParse(yearMatch.Value, out var y) && y >= 1900 && y <= DateTime.Now.Year + 2)
        {
            year = y;
        }

        var seMatch = SeasonEpisodeRegex.Match(cleanName);
        if (seMatch.Success)
        {
            season = int.Parse(seMatch.Groups[1].Value);
            episode = int.Parse(seMatch.Groups[2].Value);
            var seIndex = seMatch.Index;
            cleanName = cleanName.Substring(0, seIndex);
        }

        if (year > 0)
        {
            cleanName = YearRegex.Replace(cleanName, "").Trim();
        }

        cleanName = Regex.Replace(cleanName, @"[\.\-_]+", " ").Trim();
        cleanName = Regex.Replace(cleanName, @"\s+", " ").Trim();

        return (cleanName, year, season, episode);
    }

    public static string ExtractReleaseGroup(string fileName)
    {
        var match = ReleaseGroupRegex.Match(fileName);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public static QualityInfo ParseQuality(string fileName)
    {
        var quality = new QualityInfo();
        
        var resMatch = QualityRegex.Match(fileName);
        if (resMatch.Success)
        {
            quality.Source = NormalizeSource(resMatch.Value);
        }

        var codecMatch = CodecRegex.Match(fileName);
        if (codecMatch.Success)
        {
            quality.Codec = NormalizeCodec(codecMatch.Value);
        }

        quality.ReleaseGroup = ExtractReleaseGroup(fileName);
        quality.QualityScore = CalculateQualityScore(quality);

        return quality;
    }

    private static string NormalizeSource(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "4320P" or "8K" => "8K",
            "2160P" or "4K" or "UHD" => "4K",
            "1080P" => "1080p",
            "720P" => "720p",
            "576P" => "576p",
            "480P" => "480p",
            "HDR" => "HDR",
            "REMUX" => "Remux",
            "BLURAY" or "BDRIP" or "BRRIP" => "BluRay",
            "WEB-DL" or "WEBRIP" => "WEB-DL",
            "HDTV" or "PDTV" or "DSR" or "SATRIP" => "HDTV",
            "DVBRIP" => "DVB",
            "DVDRIP" or "DVDR" => "DVD",
            "VHSRIP" => "VHS",
            "TVRIP" => "TV",
            "HDDVD" => "HD-DVD",
            _ => source
        };
    }

    private static string NormalizeCodec(string codec)
    {
        return codec.ToUpperInvariant() switch
        {
            "X264" or "H264" or "AVC" => "x264",
            "X265" or "H265" or "HEVC" => "x265",
            "MPEG-2" => "MPEG-2",
            "VC-1" => "VC-1",
            "XVID" or "DIVX" => "XviD",
            _ => codec
        };
    }

    private static int CalculateQualityScore(QualityInfo quality)
    {
        var score = 0;
        
        score += quality.Source switch
        {
            "8K" => 1000,
            "4K" => 800,
            "1080p" => 500,
            "720p" => 300,
            "576p" => 150,
            "480p" => 100,
            "Remux" => 200,
            "BluRay" => 200,
            "WEB-DL" => 150,
            "HDTV" => 100,
            "DVD" => 50,
            _ => 0
        };

        score += quality.Codec switch
        {
            "x265" => 50,
            "x264" => 30,
            _ => 0
        };

        return score;
    }

    public static string BuildMovieFolderName(string title, int year, string? imdbId = null, string? releaseGroup = null)
    {
        var name = $"{title} ({year})";
        if (!string.IsNullOrEmpty(imdbId))
            name += $" [imdbid-{imdbId}]";
        if (!string.IsNullOrEmpty(releaseGroup))
            name += $" [{releaseGroup}]";
        return name;
    }

    public static string BuildMovieFileName(string title, int year, string? imdbId = null, string? releaseGroup = null, string extension = ".mkv")
    {
        var name = $"{title} ({year})";
        if (!string.IsNullOrEmpty(imdbId))
            name += $" [imdbid-{imdbId}]";
        if (!string.IsNullOrEmpty(releaseGroup))
            name += $" [{releaseGroup}]";
        return name + extension;
    }

    public static string BuildSeriesFolderName(string title, int year, string? imdbId = null, string? releaseGroup = null)
    {
        var name = $"{title} ({year})";
        if (!string.IsNullOrEmpty(imdbId))
            name += $" [imdbid-{imdbId}]";
        if (!string.IsNullOrEmpty(releaseGroup))
            name += $" [{releaseGroup}]";
        return name;
    }

    public static string BuildEpisodeFileName(string seriesTitle, int season, int episode, string? episodeTitle = null, string? releaseGroup = null, string extension = ".mkv")
    {
        var name = $"{seriesTitle} - S{season:D2}E{episode:D2}";
        if (!string.IsNullOrEmpty(episodeTitle))
            name += $" - {episodeTitle}";
        if (!string.IsNullOrEmpty(releaseGroup))
            name += $" [{releaseGroup}]";
        return name + extension;
    }

    public static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalidChars.Contains(c) ? '_' : c);
        }
        return sb.ToString().Replace(":", " -").TrimEnd('.', ' ');
    }

    public static (int? Season, int? Episode) ParseSeasonEpisode(string fileName)
    {
        var seMatch = SeasonEpisodeRegex.Match(fileName);
        if (seMatch.Success)
        {
            return (int.Parse(seMatch.Groups[1].Value), int.Parse(seMatch.Groups[2].Value));
        }
        return (null, null);
    }

    public static int ParseYear(string fileName)
    {
        var yearMatch = YearRegex.Match(fileName);
        if (yearMatch.Success && int.TryParse(yearMatch.Value, out var y) && y >= 1900 && y <= DateTime.Now.Year + 2)
        {
            return y;
        }
        return 0;
    }
}