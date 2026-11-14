using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
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
        serviceCollection.AddTransient<IStartupFilter, TorrentSearchStartupFilter>();
        serviceCollection.AddTransient<Controllers.TorrentSearchController>();

        serviceCollection.AddSingleton<PluginConfiguration>(_ => Plugin.Instance.Configuration);
        serviceCollection.AddSingleton<ITorrentSearchService>(sp => sp.GetRequiredService<JackettSearchService>());
        serviceCollection.AddSingleton<IDownloadManagerService>(sp =>
        {
            var config = Plugin.Instance.Configuration;
            var logger = sp.GetRequiredService<ILogger<QBittorrentDownloadService>>();

            return new QBittorrentDownloadService(config, logger);
        });
        serviceCollection.AddSingleton<ILibrarySyncService, LibrarySyncService>();
        serviceCollection.AddSingleton<QualityParser>();

        serviceCollection.AddHostedService<DownloadMonitorService>();
        serviceCollection.AddHostedService<CompletedMediaRefresh>();
        serviceCollection.AddHostedService<LibraryScannerService>();
        serviceCollection.AddHostedService<HealthCheckService>();

        serviceCollection.AddHttpClient<JackettSearchService>();
    }
}

internal sealed class TorrentSearchStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var manager = app.ApplicationServices.GetRequiredService<ApplicationPartManager>();
            var assembly = typeof(Controllers.TorrentSearchController).Assembly;
            if (!manager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == assembly))
            {
                manager.ApplicationParts.Add(new AssemblyPart(assembly));
            }

            next(app);
        };
    }
}
