using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentSearch.BackgroundServices;

public sealed class CompletedMediaRefresh(
    ILibraryManager library, ILibraryMonitor monitor, ILogger<CompletedMediaRefresh> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var queue = Plugin.Instance.Configuration.CompletedDownloadQueuePath;
            if (!Path.IsPathFullyQualified(queue) || !Directory.Exists(queue)) continue;
            try
            {
                var roots = library.GetVirtualFolders().SelectMany(folder => folder.Locations).ToArray();
                foreach (var entry in Directory.EnumerateFiles(queue, "*.ready"))
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    var path = await File.ReadAllTextAsync(entry, stoppingToken);
                    if (Path.IsPathFullyQualified(path) && Directory.Exists(path) &&
                        roots.Any(root => IsWithin(root, path)))
                    {
                        monitor.ReportFileSystemChanged(path);
                        logger.LogInformation("Reported completed download folder to Jellyfin: {Path}", path);
                    }

                    File.Delete(entry);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not process completed download notifications");
            }
        }
    }

    private static bool IsWithin(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith("../", StringComparison.Ordinal);
    }
}
