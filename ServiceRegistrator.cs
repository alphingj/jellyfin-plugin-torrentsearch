using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.TorrentSearch.Services.Search;
using Jellyfin.Plugin.TorrentSearch.Services.Download;
using Jellyfin.Plugin.TorrentSearch.Services.Library;
using Jellyfin.Plugin.TorrentSearch.BackgroundServices;
using Jellyfin.Plugin.TorrentSearch.Helpers;

namespace Jellyfin.Plugin.TorrentSearch;

public static class ServiceRegistrator
{
    public static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ITorrentSearchService, JackettSearchService>();
        services.AddSingleton<IDownloadManagerService>(sp =>
        {
            var config = Plugin.Instance.Configuration;
            var logger = sp.GetRequiredService<ILogger<QBittorrentDownloadService>>();

            return new QBittorrentDownloadService(config, logger);
        });
        services.AddSingleton<ILibrarySyncService, LibrarySyncService>();
        services.AddSingleton<QualityParser>();

        services.AddHostedService<DownloadMonitorService>();
        services.AddHostedService<LibraryScannerService>();
        services.AddHostedService<HealthCheckService>();

        services.AddHttpClient<JackettSearchService>();
    }
}