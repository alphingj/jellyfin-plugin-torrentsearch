# Changelog

## [1.0.2] - 2026-08-09
### Removed
- TMDB metadata enrichment (posters, cast, ratings, genres)
- NFO file generation and artwork download (auto-organization now renames files only)
### Changed
- Drop unused AngleSharp, pin Newtonsoft.Json 13.0.3

## [1.0.0] - 2026-07-26
### Added
- Initial release
- Search movies/TV via Jackett/Prowlarr (Torznab API)
- Download via qBittorrent WebAPI or embedded MonoTorrent (ARM64 native)
- TMDB metadata enrichment (posters, cast, ratings, genres)
- Auto-organization to Jellyfin naming convention
- NFO file generation for movies and TV shows
- Artwork download (posters, fanart)
- Dashboard UI with search, download queue, settings
- Background services for auto-sync and library scanning
- MIT License
