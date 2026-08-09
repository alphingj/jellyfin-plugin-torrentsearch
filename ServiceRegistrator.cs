using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller;
using Jellyfin.Plugin.TorrentSearch.Services.Search;
using Jellyfin.Plugin.TorrentSearch.Services.Download;
using Jellyfin.Plugin.TorrentSearch.Services.Library;
using Jellyfin.Plugin.TorrentSearch.BackgroundServices;
using Jellyfin.Plugin.TorrentSearch.Helpers;

namespace Jellyfin.Plugin.TorrentSearch;

public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ITorrentSearchService, JackettSearchService>();
        serviceCollection.AddSingleton<IDownloadManagerService>(sp =>
        {
            var config = Plugin.Instance.Configuration;
            var logger = sp.GetRequiredService<ILogger<QBittorrentDownloadService>>();

            return new QBittorrentDownloadService(config, logger);
        });
        serviceCollection.AddSingleton<ILibrarySyncService, LibrarySyncService>();
        serviceCollection.AddSingleton<QualityParser>();

        serviceCollection.AddHostedService<DownloadMonitorService>();
        serviceCollection.AddHostedService<LibraryScannerService>();
        serviceCollection.AddHostedService<HealthCheckService>();

        serviceCollection.AddHttpClient<JackettSearchService>();
    }
}