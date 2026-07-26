using System.Xml.Linq;
using Jellyfin.Plugin.TorrentSearch.Models;

namespace Jellyfin.Plugin.TorrentSearch.Helpers;

public static class NfoGenerator
{
    public static string GenerateMovieNfo(MovieMetadata movie)
    {
        var doc = new XDocument(
            new XElement("movie",
                new XElement("title", movie.Title),
                new XElement("originaltitle", movie.OriginalTitle ?? movie.Title),
                new XElement("year", movie.Year),
                new XElement("premiered", movie.ReleaseDate),
                new XElement("releasedate", movie.ReleaseDate),
                new XElement("runtime", movie.Runtime),
                new XElement("plot", movie.Overview ?? string.Empty),
                new XElement("tagline", movie.Tagline ?? string.Empty),
                new XElement("mpaa", movie.Certification ?? string.Empty),
                new XElement("rating", movie.VoteAverage.ToString("F1")),
                new XElement("votes", movie.VoteCount),
                new XElement("id", movie.ImdbId ?? string.Empty),
                new XElement("tmdbid", movie.TmdbId),
                
                movie.Genres?.Select(g => new XElement("genre", g)) ?? Enumerable.Empty<XElement>(),
                
                movie.Credits?.Cast?.Take(20).Select(c => new XElement("actor",
                    new XElement("name", c.Name),
                    new XElement("role", c.Character),
                    new XElement("order", c.Order),
                    new XElement("thumb", $"https://image.tmdb.org/t/p/w185{c.ProfilePath}"))
                ) ?? Enumerable.Empty<XElement>(),
                
                movie.Credits?.Crew?.Where(c => c.Job == "Director").Select(d => new XElement("director", d.Name)) ?? Enumerable.Empty<XElement>(),
                
                movie.Credits?.Crew?.Where(c => c.Job == "Writer" || c.Job == "Screenplay").Select(w => new XElement("credits", w.Name)) ?? Enumerable.Empty<XElement>(),
                
                movie.ProductionCompanies?.Select(p => new XElement("studio", p.Name)) ?? Enumerable.Empty<XElement>(),
                
                movie.Countries?.Select(c => new XElement("country", c.Name)) ?? Enumerable.Empty<XElement>(),
                
                movie.SpokenLanguages?.Select(l => new XElement("language", l.EnglishName)) ?? Enumerable.Empty<XElement>(),
                
                new XElement("trailer", movie.TrailerUrl ?? string.Empty)
            )
        );

        return doc.ToString(SaveOptions.OmitDuplicateNamespaces);
    }

    public static string GenerateSeriesNfo(SeriesMetadata series)
    {
        var doc = new XDocument(
            new XElement("tvshow",
                new XElement("title", series.Name),
                new XElement("sorttitle", series.Name),
                new XElement("year", series.FirstAirDate?.Year ?? 0),
                new XElement("premiered", series.FirstAirDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                new XElement("plot", series.Overview ?? string.Empty),
                new XElement("mpaa", series.ContentRating ?? string.Empty),
                new XElement("rating", series.VoteAverage.ToString("F1")),
                new XElement("votes", series.VoteCount),
                new XElement("id", series.ImdbId ?? string.Empty),
                new XElement("tmdbid", series.TmdbId),
                new XElement("status", series.Status ?? string.Empty),
                new XElement("studio", series.Networks?.FirstOrDefault()?.Name ?? string.Empty),
                
                series.Genres?.Select(g => new XElement("genre", g)) ?? Enumerable.Empty<XElement>(),
                
                series.Credits?.Cast?.Take(20).Select(c => new XElement("actor",
                    new XElement("name", c.Name),
                    new XElement("role", c.Character),
                    new XElement("order", c.Order),
                    new XElement("thumb", $"https://image.tmdb.org/t/p/w185{c.ProfilePath}"))
                ) ?? Enumerable.Empty<XElement>(),
                
                series.Credits?.Crew?.Where(c => c.Job == "Director").Select(d => new XElement("director", d.Name)) ?? Enumerable.Empty<XElement>(),
                
                series.Credits?.Crew?.Where(c => c.Job == "Writer" || c.Job == "Screenplay").Select(w => new XElement("credits", w.Name)) ?? Enumerable.Empty<XElement>(),
                
                series.Networks?.Select(n => new XElement("studio", n.Name)) ?? Enumerable.Empty<XElement>(),
                
                series.OriginCountry?.Select(c => new XElement("country", c)) ?? Enumerable.Empty<XElement>(),
                
                series.Languages?.Select(l => new XElement("language", l)) ?? Enumerable.Empty<XElement>()
            )
        );

        return doc.ToString(SaveOptions.OmitDuplicateNamespaces);
    }

    public static string GenerateEpisodeNfo(EpisodeMetadata episode, int seasonNumber, int episodeNumber)
    {
        var doc = new XDocument(
            new XElement("episodedetails",
                new XElement("title", episode.Name),
                new XElement("showtitle", episode.SeriesName),
                new XElement("season", seasonNumber),
                new XElement("episode", episodeNumber),
                new XElement("plot", episode.Overview ?? string.Empty),
                new XElement("aired", episode.AirDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                new XElement("premiered", episode.AirDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                new XElement("rating", episode.VoteAverage.ToString("F1")),
                new XElement("votes", episode.VoteCount),
                new XElement("tmdbid", episode.TmdbId),
                new XElement("director", episode.Director ?? string.Empty),
                new XElement("writer", episode.Writer ?? string.Empty),
                
                episode.GuestStars?.Select(g => new XElement("actor",
                    new XElement("name", g.Name),
                    new XElement("role", g.Character),
                    new XElement("thumb", $"https://image.tmdb.org/t/p/w185{g.ProfilePath}"))
                ) ?? Enumerable.Empty<XElement>()
            )
        );

        return doc.ToString(SaveOptions.OmitDuplicateNamespaces);
    }
}