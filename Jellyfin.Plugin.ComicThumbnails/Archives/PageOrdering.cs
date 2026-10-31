using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.ComicThumbnails.Archives;

/// <summary>
/// Selects and orders the pages of an archive exactly like the comic reader of jellyfin-web
/// (src/plugins/comicsPlayer/plugin.js), so that thumbnail N opens reader page N.
/// </summary>
internal static class PageOrdering
{
    /// <summary>
    /// Image extensions the web reader shows (IMAGE_FORMATS in jellyfin-web).
    /// </summary>
    internal static readonly HashSet<string> ImageExtensions = new(StringComparer.Ordinal)
    {
        "jpg", "jpeg", "jpe", "jif", "jfif", "jfi", "png", "avif", "gif", "bmp", "dib", "tiff", "tif", "webp"
    };

    /// <summary>
    /// Returns the file name of an archive entry, without directories.
    /// </summary>
    /// <param name="key">Entry path inside the archive.</param>
    /// <returns>The file name.</returns>
    internal static string GetFileName(string key)
    {
        var slash = key.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? key : key[(slash + 1)..];
    }

    /// <summary>
    /// Returns whether a file name is shown as a page by the web reader.
    /// </summary>
    /// <param name="fileName">File name without directories.</param>
    /// <returns>True for images.</returns>
    internal static bool IsPage(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        if (dot < 0)
        {
            return false;
        }

        return ImageExtensions.Contains(fileName[(dot + 1)..].ToLowerInvariant());
    }

    /// <summary>
    /// Filters and sorts archive entries into pages.
    /// </summary>
    /// <param name="entryKeys">Keys of all file entries, in archive order.</param>
    /// <returns>The pages in reader order.</returns>
    internal static IReadOnlyList<ComicPage> Order(IEnumerable<string> entryKeys)
    {
        // The web reader sorts by file name with JavaScript's "<", i.e. by UTF-16 code units (= ordinal),
        // and Array.prototype.sort is stable, just like LINQ's OrderBy.
        return entryKeys
            .Select(key => (Key: key, Name: GetFileName(key)))
            .Where(e => IsPage(e.Name))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .Select((e, i) => new ComicPage(i, e.Key, e.Name))
            .ToList();
    }
}
