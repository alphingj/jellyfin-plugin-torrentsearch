# Torrent Search for Jellyfin

Search Prowlarr from Jellyfin, send selected releases to qBittorrent, and manage current downloads. The plugin includes an administrator dashboard and a separate Downloads page for signed-in users. The interface defaults to English and uses German when the browser language is German.

## Requirements

- Jellyfin 12.4 for this development build
- Prowlarr and its API key for category-based search
- qBittorrent with its Web UI API enabled. API-key authentication requires qBittorrent 5.2 or later.

## Build and install

Build with the .NET 10 SDK:

```sh
dotnet build Jellyfin.Plugin.TorrentSearch.csproj -c Release
```

For manual installation, place the plugin DLL and its required runtime dependencies in a dedicated subdirectory of Jellyfin's plugins directory, then restart Jellyfin. Keep the existing plugin directory backed up when upgrading. The upstream repository manifest still points to upstream releases.

## Configure

Open **Dashboard → Plugins → Torrent Search**. Enter the Prowlarr URL and API key, and configure the qBittorrent URL and authentication. Use addresses reachable from the Jellyfin server. Keep API keys in plugin settings.

- **Search categories:** Movies and TV Shows can be renamed, reassigned, or hidden. Custom cards can include multiple Prowlarr categories, exclude categories, and expose season and episode fields. Latest releases depends on whether the indexer supports queries without a title.
- **Download routing:** Disable the category prompt to hand the selected release to Prowlarr and use its configured download-client mapping.
- **Downloads page:** Open `/TorrentSearch/UserDownloads/Page` on the Jellyfin server. To add a navigation entry, configure a [Jellyfin Web menu link](https://jellyfin.org/docs/general/clients/web-config/#custom-menu-links) pointing to that path. Client support for these links varies.
- **Library search:** The Downloads page and admin search show matching local library items alongside the search workflow. The Downloads page can label matching movies and series. A series match does not establish that a particular episode is present.
- **Queue permissions:** Administrators can manage all shown downloads. Other users can manage torrents assigned to them after a start from the Downloads page. Torrents without an established owner appear as **System**. Removing a torrent through the Downloads page preserves its files.

## Refresh libraries after downloads

Set `CompletedDownloadQueuePath` in the plugin's XML configuration to an absolute directory writable by Jellyfin and the download completion hook. Create the directory before enabling the hook. After the download or extraction succeeds, have the hook write the completed media directory (as Jellyfin sees it) to a temporary UTF-8 file in the queue, then rename it with the `.ready` suffix. Jellyfin consumes these notifications for existing paths under configured libraries. Leave the setting empty to disable the queue.

## Credits and license

- **alphingj** ([@alphingj](https://github.com/alphingj)) — original author and upstream maintainer
- **Jakob Heinemann** — contributor to the Jellyfin 12.4 port, Prowlarr integration, and Downloads interface

Distributed under the [MIT License](LICENSE).
