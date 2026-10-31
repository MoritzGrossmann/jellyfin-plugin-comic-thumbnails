using System;
using System.Globalization;

namespace Jellyfin.Plugin.ComicThumbnails.Web;

/// <summary>
/// Adds the client script tag to the web client's index.html.
/// </summary>
internal static class HtmlInjector
{
    /// <summary>
    /// Marker that identifies the injected tag, so it is never added twice.
    /// </summary>
    internal const string Marker = "data-comic-thumbnails";

    /// <summary>
    /// Builds the script tag. The path is relative to /web/index.html, so it works with a base URL
    /// and behind reverse proxies that serve Jellyfin under a sub path.
    /// </summary>
    /// <param name="version">Plugin version, used to bust the browser cache after updates.</param>
    /// <returns>The script tag.</returns>
    internal static string BuildScriptTag(string version)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "<script {0} defer src=\"../ComicThumbnails/ClientScript?v={1}\"></script>",
            Marker,
            Uri.EscapeDataString(version));
    }

    /// <summary>
    /// Inserts the tag before &lt;/body&gt;, or appends it if there is none.
    /// </summary>
    /// <param name="html">The page.</param>
    /// <param name="scriptTag">The tag to insert.</param>
    /// <returns>The changed page; unchanged if the tag is already present.</returns>
    internal static string Inject(string html, string scriptTag)
    {
        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return html;
        }

        var body = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return body < 0 ? html + scriptTag : html.Insert(body, scriptTag);
    }

    /// <summary>
    /// Returns whether a request path points to the web client's index.html.
    /// </summary>
    /// <param name="path">Request path, including a base URL if one is configured.</param>
    /// <returns>True for /web/ and /web/index.html.</returns>
    internal static bool IsIndexPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
    }
}
