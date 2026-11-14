# Changelog

## Unreleased

### Added
- Prowlarr category discovery, configurable search cards, included and excluded categories, and latest-release queries.
- A signed-in Downloads page with search across categories, live progress, pause and resume, and confirmed removal of owned torrents without deleting files.
- Local Jellyfin library matches and Prowlarr indexer health notices.
- German translations with English as the default.

### Changed
- Target Jellyfin 12.4 and .NET 10.
- Support qBittorrent API-key authentication and updated torrent states and actions.
- Allow Prowlarr to apply its configured download-client mapping when the category prompt is disabled.

- Add an optional filesystem queue for refreshing matching libraries after download or extraction.

## [1.0.3] - 2026-08-09
### Fixed
- Plugin DI registration — services now properly register via `IPluginServiceRegistrator` (panel was broken since initial release)
### Removed
- Unused `MediatR` and `FluentValidation` package dependencies

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
