using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TorrentSearch;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin Instance { get; private set; } = null!;

    public override Guid Id => Guid.Parse("8b9e2c4d-7f3a-4d5e-9c1b-2a8f6e4d3c7a");

    public override string Name => "Torrent Search";

    public override string Description => "Search for movies and TV shows via Jackett/Prowlarr, download via qBittorrent or embedded MonoTorrent, and auto-sync to Jellyfin library";

    public override string ConfigurationFileName => "configuration.xml";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "Torrent Search",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html",
            EnableInMainMenu = true,
            DisplayName = "Torrent Search"
        };
    }
}