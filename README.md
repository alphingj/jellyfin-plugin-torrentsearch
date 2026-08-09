# Jellyfin Torrent Search Plugin

A Jellyfin plugin that lets you search for movies and TV shows via Jackett/Prowlarr, download torrents via qBittorrent (or embedded MonoTorrent), and automatically organize them into your Jellyfin library.

![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11%2B-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Search** - Search movies and TV shows via Jackett/Prowlarr (500+ indexers via Torznab API)
- **Quality Detection** - Automatically detects quality (4K/1080p/720p, BluRay/WEB-DL, x264/x265, HDR, etc.)
- **Download** - qBittorrent WebAPI integration (with embedded MonoTorrent fallback for ARM64)
- **Auto-Organization** - Renames/moves files to Jellyfin naming convention
- **Library Sync** - Triggers Jellyfin library refresh automatically
- **Dashboard UI** - Vue 3 + Alpine.js dashboard with search, download queue, and settings

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Jellyfin Plugin                            │
├─────────────────────────────────────────────────────────────────┤
│  Search (Jackett) ──▶ Download (qBittorrent) ──▶ Library Sync    │
│       │                    │                    │                │
│       ▼                    ▼                    ▼                │
│  Torznab API          WebAPI / Embedded     Rename + refresh     │
│  500+ indexers        MonoTorrent (ARM64)   Jellyfin naming      │
│                                                   │              │
└───────────────────────────────────────────────────│──────────────┘
                                                     ▼
                              ┌─────────────────────────────────────┐
                              │        Library Sync                 │
                              │  • Rename to Jellyfin convention    │
                              │  • Trigger Jellyfin refresh         │
                              └─────────────────────────────────────┘
```

## Prerequisites

- **Jellyfin 10.11+**
- **Jackett** (port 9117) or **Prowlarr** (port 9696) - for torrent searching
- **qBittorrent** (port 8080) - recommended for downloads
  - *Alternative:* Embedded MonoTorrent (no external client needed, ARM64 native)

## Installation

### Option 1: Manual (Recommended)
1. Download the latest `Jellyfin.Plugin.TorrentSearch.dll` from [Releases](https://github.com/alphingj/jellyfin-plugin-torrentsearch/releases)
2. Copy to your Jellyfin plugins folder:
   ```bash
   # Linux/Docker
   /config/plugins/Jellyfin.Plugin.TorrentSearch/
   
   # Windows
   C:\ProgramData\jellyfin\plugins\Jellyfin.Plugin.TorrentSearch\
   ```
3. Restart Jellyfin
4. Go to **Dashboard → Torrent Search** to configure

### Option 2: From Source
```bash
git clone https://github.com/alphingj/jellyfin-plugin-torrentsearch.git
cd jellyfin-plugin-torrentsearch/Jellyfin.Plugin.TorrentSearch
dotnet publish -c Release -r linux-x64 --self-contained false
# Copy bin/Release/net9.0/linux-x64/publish/Jellyfin.Plugin.TorrentSearch.dll to Jellyfin plugins
```

## Configuration

Navigate to **Dashboard → Torrent Search** and configure:

### Indexer (Jackett/Prowlarr)
| Setting | Description |
|---------|-------------|
| Jackett URL | `http://localhost:9117` (default) |
| Jackett API Key | From Jackett UI |
| Use Prowlarr | Toggle for Prowlarr instead |
| Prowlarr URL | `http://localhost:9696` |
| Prowlarr API Key | From Prowlarr UI |

### Torrent Client
| Setting | Description |
|---------|-------------|
| Client Type | qBittorrent (recommended) or MonoTorrent (embedded) |
| qBittorrent URL | `http://localhost:8080` |
| Username/Password | qBittorrent WebUI credentials |
| Download Path | Where torrents download before organization |

### Search Preferences
| Setting | Default |
|---------|---------|
| Max Results | 20 |
| Min Seeders | 1 |
| Preferred Keywords | `1080p,x264,x265,hevc` |
| Excluded Keywords | `cam,ts,tc,scr,dvdscr,r5` |

### Library Paths
| Setting | Default |
|---------|---------|
| Movies Path | `/media/movies` |
| TV Shows Path | `/media/tv` |
| Auto Refresh | Enabled |
| Scan Interval | 5 minutes |

## Usage

### Search & Download
1. Go to **Torrent Search → Search tab**
2. Select **Movies** or **TV Shows**
3. Enter query (e.g., "The Matrix 1999")
4. Results show: title, year, quality badges, seeders/leechers, size, indexer
5. Click **Download** → confirm path/category → torrent added to client

### Download Queue
- **Torrent Search → Downloads tab** shows active downloads
- Progress bar, speed, seeders/peers, ETA
- Pause/Resume/Remove controls

### Library Sync
- Automatic: After download completes, files are organized and Jellyfin refreshed
- Manual: **Torrent Search → Library** tab → "Scan Movies" / "Scan TV Shows"

## Jellyfin Naming Convention

Files are organized as:

**Movies:**
```
/media/movies/
└── Movie Title (Year)/
    └── Movie Title (Year) [Quality].mkv
```

**TV Shows:**
```
/media/tv/
└── Show Name (Year)/
    ├── Season 01/
    │   └── Show Name - S01E01 [Quality].mkv
    ├── Season 02/
    │   └── ...
```

## Docker Compose (Development)

```yaml
services:
  jellyfin:
    image: jellyfin/jellyfin:latest
    volumes:
      - ./jellyfin-config:/config
      - ./plugins:/config/plugins
      - ./media:/media
    ports:
      - "8096:8096"

  jackett:
    image: lscr.io/linuxserver/jackett:latest
    volumes:
      - ./jackett-config:/config
    ports:
      - "9117:9117"

  qbittorrent:
    image: lscr.io/linuxserver/qbittorrent:latest
    volumes:
      - ./qbittorrent-config:/config
      - ./downloads:/downloads
    ports:
      - "8080:8080"
    environment:
      - WEBUI_PORT=8080
```

## Building

```bash
# Prerequisites: .NET 9.0 SDK
git clone https://github.com/alphingj/jellyfin-plugin-torrentsearch.git
cd jellyfin-plugin-torrentsearch/Jellyfin.Plugin.TorrentSearch

# Debug build
dotnet build

# Release build (for distribution)
dotnet publish -c Release -o ./publish

# Run tests (when added)
dotnet test
```

## Project Structure

```
Jellyfin.Plugin.TorrentSearch/
├── Plugin.cs                      # Main plugin entry (IHasWebPages)
├── PluginConfiguration.cs         # All user settings
├── ServiceRegistrator.cs          # DI registration
├── Controllers/
│   └── TorrentSearchController.cs # REST API endpoints
├── Services/
│   ├── Search/
│   │   ├── ITorrentSearchService.cs
│   │   └── JackettSearchService.cs    # Torznab/Jackett
│   ├── Download/
│   │   ├── IDownloadManagerService.cs
│   │   ├── QBittorrentDownloadService.cs
│   │   └── MonoTorrentDownloadService.cs # Embedded fallback
│   └── Library/
│       ├── ILibrarySyncService.cs
│       └── LibrarySyncService.cs      # File org + refresh
├── BackgroundServices/
│   └── BackgroundServices.cs      # Download monitor, library scanner
├── Helpers/
│   ├── MediaNamingHelper.cs       # Jellyfin naming + parsing
│   └── QualityParser.cs           # Quality detection from filename
├── Models/
│   └── TorrentModels.cs           # All DTOs
└── Configuration/
    ├── config.html                # Vue 3 + Alpine.js dashboard
    └── config.css                 # Embedded styling
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/TorrentSearch/Search/Movie` | Search movies |
| GET | `/TorrentSearch/Search/Show` | Search TV shows |
| POST | `/TorrentSearch/Search` | Advanced search |
| GET | `/TorrentSearch/Search/Autocomplete` | Search suggestions |
| POST | `/TorrentSearch/Download/Start` | Start magnet download |
| POST | `/TorrentSearch/Download/AddTorrent` | Add .torrent file |
| GET | `/TorrentSearch/Downloads` | List active downloads |
| GET | `/TorrentSearch/Downloads/{hash}` | Get download status |
| POST | `/TorrentSearch/Downloads/{hash}/Pause` | Pause download |
| POST | `/TorrentSearch/Downloads/{hash}/Resume` | Resume download |
| DELETE | `/TorrentSearch/Downloads/{hash}` | Remove download |
| POST | `/TorrentSearch/Library/Sync` | Trigger library scan |
| POST | `/TorrentSearch/Library/Refresh` | Refresh Jellyfin library |
| GET | `/TorrentSearch/Config/TestIndexer` | Test Jackett connection |
| GET | `/TorrentSearch/Config/TestClient` | Test qBittorrent connection |

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

Distributed under the MIT License. See `LICENSE` for more information.

## Author

**alphingj**
- GitHub: [@alphingj](https://github.com/alphingj)
- LinkedIn: [alphingj](https://in.linkedin.com/in/alphingj)

## Acknowledgments

- [Jellyfin](https://jellyfin.org/) - Amazing media server
- [Jackett](https://github.com/Jackett/Jackett) - Torrent indexer proxy
- [qBittorrent](https://www.qbittorrent.org/) - Torrent client
- [MonoTorrent](https://github.com/alanmcgovern/monotorrent) - Embedded BitTorrent library
- [Vue.js](https://vuejs.org/) & [Alpine.js](https://alpinejs.dev/) - Dashboard UI