using Jellyfin.Plugin.ComicThumbnails.Archives;
using Jellyfin.Plugin.ComicThumbnails.Thumbnails;
using Jellyfin.Plugin.ComicThumbnails.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ComicThumbnails;

/// <summary>
/// Registers the plugin's services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ComicPageReader>();
        serviceCollection.AddSingleton<ThumbnailService>();

        // Adds the client script to the web client's index.html, without touching files on disk.
        serviceCollection.AddTransient<IStartupFilter, ScriptInjectionStartupFilter>();
    }
}
