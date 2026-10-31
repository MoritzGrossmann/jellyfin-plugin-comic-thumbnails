using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.ComicThumbnails.Web;

/// <summary>
/// Adds the client script to the web client's index.html while it is served.
/// Nothing is written to disk, so this also works with read-only installs (e.g. Docker).
/// </summary>
public sealed class ScriptInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ScriptInjectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptInjectionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="logger">Logger.</param>
    public ScriptInjectionMiddleware(RequestDelegate next, ILogger<ScriptInjectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var plugin = Plugin.Instance;
        if (plugin is null
            || !plugin.Configuration.InjectClientScript
            || !HttpMethods.IsGet(context.Request.Method)
            || !HtmlInjector.IsIndexPath(context.Request.Path.Value))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // The page must arrive uncompressed and in full (no 304), otherwise it cannot be changed.
        context.Request.Headers.Remove(HeaderNames.AcceptEncoding);
        context.Request.Headers.Remove(HeaderNames.IfNoneMatch);
        context.Request.Headers.Remove(HeaderNames.IfModifiedSince);

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var isHtml = context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false;
        if (context.Response.StatusCode != StatusCodes.Status200OK || !isHtml)
        {
            await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        byte[] output;
        try
        {
            var html = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            var tag = HtmlInjector.BuildScriptTag(plugin.Version.ToString());
            output = Encoding.UTF8.GetBytes(HtmlInjector.Inject(html, tag));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not add the Comic Thumbnails script to index.html");
            output = buffer.ToArray();
        }

        context.Response.ContentLength = output.Length;
        context.Response.Headers.Remove(HeaderNames.ETag);
        context.Response.Headers.Remove(HeaderNames.LastModified);
        await originalBody.WriteAsync(output, context.RequestAborted).ConfigureAwait(false);
    }
}
