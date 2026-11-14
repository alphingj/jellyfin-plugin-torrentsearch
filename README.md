# Jellyfin Torrent Search Plugin

Search Jackett or Prowlarr from Jellyfin, send releases to qBittorrent, and manage downloads. This fork targets Jellyfin 12.4 and includes an administrator dashboard and a Downloads page for signed-in users.

![Jellyfin](https://img.shields.io/badge/Jellyfin-12.4-blue)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Search** - Search movies and TV shows via Jackett/Prowlarr (500+ indexers via Torznab API)
- **Quality Detection** - Automatically detects quality (4K/1080p/720p, BluRay/WEB-DL, x264/x265, HDR, etc.)
- **Download** - qBittorrent Web API integration, with optional Prowlarr routing
- **Auto-Organization** - Renames/moves files to Jellyfin naming convention
- **Library Sync** - Triggers Jellyfin library refresh automatically
- **Dashboard UI** - Admin search, download queue, and settings, plus a separate Downloads page

## Architecture

The admin dashboard and Downloads page are embedded HTML, CSS and JavaScript. The plugin sends searches to Jackett (Torznab) or Prowlarr (REST API). Downloads go to qBittorrent directly or through Prowlarr's configured client. Jellyfin handles library refresh.

## Prerequisites

- **Jellyfin 12.4** for this fork branch
- **Jackett** (port 9117) or **Prowlarr** (port 9696) - for torrent searching
- **qBittorrent** with the Web UI enabled (default port 8080). Optional API-key authentication requires qBittorrent 5.2 or later.


## Installation

For this branch, build the package from source with the .NET 10 SDK using the commands under [Building](#building). Copy the plugin DLL and its published runtime dependencies into a dedicated folder under Jellyfin's plugins directory (for example, `/config/plugins/TorrentSearch/` in Docker). Restart Jellyfin and open **Dashboard → Torrent Search**.

The [original project's releases](https://github.com/alphingj/jellyfin-plugin-torrentsearch/releases) are upstream builds. Check their Jellyfin compatibility before installing them.

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
| Client Type | qBittorrent is the registered download service in this branch |
| qBittorrent URL | `http://localhost:8080` |
| Username/Password | qBittorrent WebUI credentials |
| qBittorrent API Key | Optional. Generate a key under **qBittorrent → Settings → Web UI → API Key** (5.2+), enter it in the plugin, save, and restart Jellyfin. |
| Download Path | Stored setting. Actual destination comes from the download dialog or the client routing. |
| Max Concurrent Downloads | Stored setting (default 3). Configure actual queue limits in qBittorrent. |
| Ask for the qBittorrent category before downloading | Enabled by default. In the admin search, turn it off to use Prowlarr's download-client category mapping for Prowlarr releases. |

### Search Preferences
| Setting | Default |
|---------|---------|
| Max Results | 20 |
| Min Seeders | 1 |
| Preferred Keywords | `1080p,x264,x265,hevc` |
| Excluded Keywords | `cam,ts,tc,scr,dvdscr,r5` |

#### Prowlarr search cards

With Prowlarr enabled, open **Custom search categories** in the plugin settings:

- **Movies** and **TV Shows** start with Prowlarr categories **Movies (2000)** and **TV (5000)**. You can rename or hide either card and change its category mapping.
- Select **Add category** to create a search card with your own name, such as **Music** or **Documentaries**. You can also enable season and episode fields on a custom card.
- Each card can search multiple Prowlarr categories and exclude categories. For example, select **TV (5000)** and exclude **TV/Documentary (5080)** to include general TV releases while filtering out releases marked as documentaries.

Save the settings to update the cards in the admin search.

### Library Paths
| Setting | Default |
|---------|---------|
| Movies Path | `/media/movies` |
| TV Shows Path | `/media/tv` |
| Automatically import completed downloads (direct clients) | Enabled by default. The direct-client monitor skips Prowlarr mode. |
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
- **Torrent Search → Downloads** lists qBittorrent torrents and offers an active-download filter.
- The signed-in **Downloads** page lists incomplete torrents, including paused ones, with live progress, speed, and owner. Users can pause, resume, or remove their assigned torrents. Administrators can manage all entries. Unassigned torrents appear as **System**. Removing an entry here keeps downloaded files.

### Library Sync
- Automatic: Direct-client import and the optional [completion hook](#optional-library-refresh) are configured separately. The hook can also refresh media added through Prowlarr.
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
    image: jellyfin/jellyfin:12.4.0
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
      - ./media:/media
    ports:
      - "8080:8080"
    environment:
      - WEBUI_PORT=8080
```

## Building

With the .NET 10 SDK installed:

```bash
git clone --branch jellyfin-12.4 https://github.com/SnakyJake/jellyfin-plugin-torrentsearch.git
cd jellyfin-plugin-torrentsearch
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

## Project Structure

```
Jellyfin.Plugin.TorrentSearch/
├── Plugin.cs                      # Main plugin entry (IHasWebPages)
├── PluginConfiguration.cs         # All user settings
├── ServiceRegistrator.cs          # DI registration
├── Controllers/
│   ├── TorrentSearchController.cs # Administrator API
│   └── UserDownloadsController.cs # Signed-in downloads API and page
├── Services/
│   ├── Search/
│   │   ├── ITorrentSearchService.cs
│   │   └── JackettSearchService.cs    # Jackett and Prowlarr searches
│   ├── Download/
│   │   ├── IDownloadManagerService.cs
│   │   └── QBittorrentDownloadService.cs
│   └── Library/
│       ├── ILibrarySyncService.cs
│       └── LibrarySyncService.cs      # File org + refresh
├── BackgroundServices/
│   ├── BackgroundServices.cs      # Download monitor, scanner and health checks
│   └── CompletedMediaRefresh.cs   # Completion notification queue
├── Helpers/
│   ├── MediaNamingHelper.cs       # Jellyfin naming + parsing
│   └── QualityParser.cs           # Quality detection from filename
├── Models/
│   └── TorrentModels.cs           # All DTOs
└── Configuration/
    ├── config.html                # Administrator dashboard
    ├── downloads.html             # Signed-in Downloads page
    └── config.css                 # Embedded styling
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/TorrentSearch/Search/Movie` | Search movies |
| GET | `/TorrentSearch/Search/Show` | Search TV shows |
| POST | `/TorrentSearch/Search` | Advanced search |
| GET | `/TorrentSearch/Search/Autocomplete` | Search suggestions |
| GET | `/TorrentSearch/Search/Categories` | List Prowlarr search categories |
| GET | `/TorrentSearch/Download/Categories` | List qBittorrent destination categories |
| POST | `/TorrentSearch/Download/Start` | Start a Prowlarr release or magnet download |
| POST | `/TorrentSearch/Download/AddTorrent` | Add .torrent file |
| GET | `/TorrentSearch/Downloads` | List qBittorrent downloads |
| GET | `/TorrentSearch/Downloads/{hash}` | Get download status |
| POST | `/TorrentSearch/Downloads/{hash}/Pause` | Pause download |
| POST | `/TorrentSearch/Downloads/{hash}/Resume` | Resume download |
| DELETE | `/TorrentSearch/Downloads/{hash}` | Remove download |
| POST | `/TorrentSearch/Library/Sync` | Trigger library scan |
| POST | `/TorrentSearch/Library/Refresh` | Refresh Jellyfin library |
| GET | `/TorrentSearch/Config/TestIndexer` | Test Jackett connection |
| GET | `/TorrentSearch/Config/TestClient` | Test qBittorrent connection |

The routes above require administrator access. The Downloads page is served at `GET /TorrentSearch/UserDownloads/Page`. Its API requires a Jellyfin sign-in:

| Method | Path under `/TorrentSearch/UserDownloads` | Description |
|---|---|---|
| GET | `/Search` | Search Prowlarr across categories |
| GET | `/Status` | Check whether Prowlarr reports an indexer issue |
| POST | `/Start` | Send a selected release to Prowlarr |
| GET | Base path | List incomplete torrents and their owners |
| POST | `/{hash}/Pause`, `/{hash}/Resume` | Pause or resume a torrent the caller can manage |
| DELETE | `/{hash}` | Remove a torrent the caller can manage, preserving its files |

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

Distributed under the MIT License. See `LICENSE` for more information.

## Authors

**alphingj**
- GitHub: [@alphingj](https://github.com/alphingj)
- LinkedIn: [alphingj](https://in.linkedin.com/in/alphingj)

**Jakob Heinemann**
- GitHub: [@SnakyJake](https://github.com/SnakyJake)
- Jellyfin 12.4 port, Prowlarr integration, and Downloads interface

## Acknowledgments

- [Jellyfin](https://jellyfin.org/) - Amazing media server
- [Jackett](https://github.com/Jackett/Jackett) - Torrent indexer proxy
- [qBittorrent](https://www.qbittorrent.org/) - Torrent client
- [MonoTorrent](https://github.com/alanmcgovern/monotorrent) - Embedded BitTorrent library


## Jellyfin 12.4 fork

- Configurable Prowlarr search cards with category inclusion and exclusion.
- A signed-in Downloads page at `/TorrentSearch/UserDownloads/Page` with search, local library matches, and live download progress.
- Administrators can manage all torrents. Other users can manage their assigned torrents. Removing an entry preserves downloaded files.
- qBittorrent API-key support and Prowlarr download routing when the category prompt is disabled.
- English by default, with German translations for German browser languages.

### Configure the fork

1. Open **Dashboard → Plugins → Torrent Search**. Enable **Use Prowlarr instead of Jackett**, enter the Prowlarr base URL and API key, and test the indexer connection.
2. Under **Torrent Client Settings**, select qBittorrent and enter its Web UI base URL. Provide the Web UI username and password or an API key (qBittorrent 5.2+). Restart Jellyfin after changing the API key, then test the client connection.
3. Under **Custom search categories**, configure the built-in cards and add your own. Each card can include and exclude multiple Prowlarr categories. Save the configuration.
4. To use Prowlarr's download routing in the admin search, disable **Ask for the qBittorrent category before downloading**. Configure qBittorrent and its category mappings in Prowlarr. Downloads started from the signed-in Downloads page use Prowlarr's routing.

Use URLs reachable from the Jellyfin server. For containers on the same Docker network, use the service names and internal ports.

### Optional Jellyfin Web menu link

Open `/TorrentSearch/UserDownloads/Page` on your Jellyfin server while signed in on the same browser and server address. To expose the page as **Downloads** in the navigation, merge this entry into the existing `menuLinks` array in Jellyfin Web's `config.json`:

```json
{
  "menuLinks": [
    {
      "name": "Downloads",
      "url": "/TorrentSearch/UserDownloads/Page",
      "icon": "download"
    }
  ]
}
```

If Jellyfin uses a base URL such as `/jellyfin`, prefix the link path accordingly. For Docker, bind-mount the modified file so that it survives container recreation. Menu visibility varies by client. Jellyfin Web opens the link in a separate tab. See the [Jellyfin Web configuration guide](https://jellyfin.org/docs/general/clients/web-config/#custom-menu-links).

### Optional library refresh


For library refresh after download or extraction, set `CompletedDownloadQueuePath` in the plugin XML to a shared absolute directory. After completion, the hook writes the media directory as seen by Jellyfin to a temporary UTF-8 file and renames it with a `.ready` suffix. Leave the setting empty to disable this integration.

See the [changelog](CHANGELOG.md) for details.
