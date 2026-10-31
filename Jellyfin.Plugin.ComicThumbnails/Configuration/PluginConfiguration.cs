using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ComicThumbnails.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Smallest allowed page size.
    /// </summary>
    public const int MinPageSize = 6;

    /// <summary>
    /// Largest allowed page size.
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// Smallest allowed thumbnail width.
    /// </summary>
    public const int MinThumbnailWidth = 80;

    /// <summary>
    /// Largest allowed thumbnail width.
    /// </summary>
    public const int MaxThumbnailWidth = 800;

    /// <summary>
    /// Gets or sets a value indicating whether the client script is injected into the web client's index.html.
    /// </summary>
    public bool InjectClientScript { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of thumbnails shown per page of the thumbnail section.
    /// </summary>
    public int PageSize { get; set; } = 24;

    /// <summary>
    /// Gets or sets the width of the generated thumbnails in pixels.
    /// </summary>
    public int ThumbnailWidth { get; set; } = 240;

    /// <summary>
    /// Gets or sets the JPEG/WebP quality of the generated thumbnails (1 to 100).
    /// </summary>
    public int ThumbnailQuality { get; set; } = 80;

    /// <summary>
    /// Gets or sets a value indicating whether the section starts collapsed.
    /// </summary>
    public bool StartCollapsed { get; set; }
}
