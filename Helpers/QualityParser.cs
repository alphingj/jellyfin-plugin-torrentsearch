using System.Text.RegularExpressions;
using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Helpers;

public class QualityParser
{
    private static readonly Regex ResolutionRegex = new(
        @"\b(4320p|2160p|1080p|720p|576p|480p|8K|4K|UHD|HDR|REMUX|BLURAY|BDRip|BRRip|WEB-DL|WEBRip|HDTV|PDTV|DSR|SATRip|DVBRip|DVDRip|DVDR|VHSRip|TVRip|HDDVD)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex CodecRegex = new(
        @"\b(x264|x265|h264|h265|HEVC|AVC|MPEG-2|VC-1|XviD|DivX)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex SourceRegex = new(
        @"\b(BluRay|BDRip|BRRip|WEB-DL|WEBRip|WEB|HDTV|PDTV|DSR|SATRip|DVBRip|DVDRip|DVDR|VHSRip|TVRip|HDDVD)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex HdrRegex = new(
        @"\b(HDR|HDR10|HDR10\+|Dolby\s*Vision|DV|HLG)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex AudioRegex = new(
        @"\b(Atmos|TrueHD|DTS-HD|DTS:X|DDP|DD\+|AC3|EAC3|AAC|FLAC|MP3|Opus|DTS)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex ReleaseGroupRegex = new(
        @"[-_\.]([A-Za-z0-9]+)$",
        RegexOptions.Compiled);

    public QualityInfo ParseQuality(string filename)
    {
        var quality = new QualityInfo();
        
        var resolutionMatch = ResolutionRegex.Match(filename);
        if (resolutionMatch.Success)
        {
            quality.Resolution = NormalizeResolution(resolutionMatch.Value);
        }

        var codecMatch = CodecRegex.Match(filename);
        if (codecMatch.Success)
        {
            quality.Codec = NormalizeCodec(codecMatch.Value);
        }

        var sourceMatch = SourceRegex.Match(filename);
        if (sourceMatch.Success)
        {
            quality.Source = NormalizeSource(sourceMatch.Value);
        }

        quality.IsHDR = HdrRegex.IsMatch(filename);

        var audioMatch = AudioRegex.Match(filename);
        if (audioMatch.Success)
        {
            quality.Audio = audioMatch.Value.ToUpperInvariant();
        }

        var releaseGroupMatch = ReleaseGroupRegex.Match(filename);
        if (releaseGroupMatch.Success)
        {
            quality.ReleaseGroup = releaseGroupMatch.Groups[1].Value;
        }

        quality.QualityScore = CalculateQualityScore(quality);
        
        return quality;
    }

    private string NormalizeResolution(string resolution)
    {
        return resolution.ToUpperInvariant() switch
        {
            "8K" => "8K",
            "4K" or "2160P" or "UHD" => "4K",
            "1080P" => "1080p",
            "720P" => "720p",
            "576P" => "576p",
            "480P" => "480p",
            _ => resolution
        };
    }

    private string NormalizeCodec(string codec)
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

    private string NormalizeSource(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "BLURAY" or "BDRIP" or "BRRIP" => "BluRay",
            "WEB-DL" or "WEBRIP" or "WEB" => "WEB-DL",
            "HDTV" or "PDTV" or "DSR" or "SATRIP" => "HDTV",
            "DVBRIP" => "DVB",
            "DVDRIP" or "DVDR" => "DVD",
            "VHSRIP" => "VHS",
            "TVRIP" => "TV",
            "HDDVD" => "HD-DVD",
            _ => source
        };
    }

    private int CalculateQualityScore(QualityInfo quality)
    {
        var score = 0;
        
        score += quality.Resolution switch
        {
            "8K" => 1000,
            "4K" => 800,
            "1080p" => 500,
            "720p" => 300,
            "576p" => 150,
            "480p" => 100,
            _ => 0
        };

        score += quality.Source switch
        {
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

        if (quality.IsHDR) score += 100;

        return score;
    }
}