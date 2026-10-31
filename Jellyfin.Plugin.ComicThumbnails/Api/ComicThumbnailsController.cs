using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ComicThumbnails.Archives;
using Jellyfin.Plugin.ComicThumbnails.Configuration;
using Jellyfin.Plugin.ComicThumbnails.Thumbnails;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicThumbnails.Api;

/// <summary>
/// Page list and page thumbnails of comics, plus the web client script.
/// </summary>
[ApiController]
[Route("ComicThumbnails")]
public class ComicThumbnailsController : ControllerBase
{
    // Claim in which Jellyfin stores the id of the authenticated user.
    private const string UserIdClaim = "Jellyfin-UserId";

    private const string ScriptResource = "Jellyfin.Plugin.ComicThumbnails.Web.comicThumbnails.js";

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ComicPageReader _reader;
    private readonly ThumbnailService _thumbnails;
    private readonly ILogger<ComicThumbnailsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComicThumbnailsController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="reader">The archive reader.</param>
    /// <param name="thumbnails">The thumbnail service.</param>
    /// <param name="logger">Logger.</param>
    public ComicThumbnailsController(
        ILibraryManager libraryManager,
        IUserManager userManager,
        ComicPageReader reader,
        ThumbnailService thumbnails,
        ILogger<ComicThumbnailsController> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _reader = reader;
        _thumbnails = thumbnails;
        _logger = logger;
    }

    private static PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Lists the pages of a comic, one pager page at a time.
    /// </summary>
    /// <param name="itemId">Id of the comic.</param>
    /// <param name="startIndex">Index of the first page to return.</param>
    /// <param name="limit">Number of pages to return; defaults to the configured page size.</param>
    /// <returns>The pages. TotalCount is 0 for items that are not comic archives.</returns>
    [HttpGet("{itemId}/Pages")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PageListResult> GetPages([FromRoute] Guid itemId, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null)
    {
        var config = Config;
        var pageSize = Math.Clamp(limit ?? config.PageSize, 1, PluginConfiguration.MaxPageSize);
        var result = new PageListResult
        {
            ItemId = itemId,
            StartIndex = Math.Max(0, startIndex),
            Limit = pageSize,
            StartCollapsed = config.StartCollapsed
        };

        var item = GetItem(itemId);
        if (item is null)
        {
            return NotFound();
        }

        if (!ComicPageReader.IsSupported(item.Path) || !System.IO.File.Exists(item.Path))
        {
            return result;
        }

        try
        {
            var pages = _reader.GetPages(item.Path);
            result.TotalCount = pages.Count;
            result.Pages = pages
                .Skip(result.StartIndex)
                .Take(pageSize)
                .Select(p => new PageInfo { Index = p.Index, Name = p.Name })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the pages of {Path}", item.Path);
        }

        return result;
    }

    /// <summary>
    /// Returns the thumbnail of a page.
    /// </summary>
    /// <param name="itemId">Id of the comic.</param>
    /// <param name="index">Zero-based page index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The image.</returns>
    [HttpGet("{itemId}/Pages/{index}/Thumbnail")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetThumbnail([FromRoute] Guid itemId, [FromRoute] int index, CancellationToken cancellationToken)
    {
        var item = GetItem(itemId);
        if (item is null || !ComicPageReader.IsSupported(item.Path) || !System.IO.File.Exists(item.Path))
        {
            return NotFound();
        }

        string path;
        try
        {
            var pages = _reader.GetPages(item.Path);
            if (index < 0 || index >= pages.Count)
            {
                return NotFound();
            }

            var config = Config;
            var width = Math.Clamp(config.ThumbnailWidth, PluginConfiguration.MinThumbnailWidth, PluginConfiguration.MaxThumbnailWidth);
            var quality = Math.Clamp(config.ThumbnailQuality, 1, 100);
            path = await _thumbnails.GetThumbnailAsync(item, pages[index], width, quality, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create the thumbnail of page {Index} of {Path}", index, item.Path);
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=86400";
        return PhysicalFile(path, GetContentType(path));
    }

    /// <summary>
    /// Deletes all cached thumbnails.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpDelete("Cache")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult ClearCache()
    {
        _thumbnails.ClearCache();
        return NoContent();
    }

    /// <summary>
    /// Returns the script that adds the thumbnail section to the web client.
    /// </summary>
    /// <returns>The JavaScript file.</returns>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetClientScript()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ScriptResource);
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "application/javascript; charset=utf-8");
    }

    private BaseItem? GetItem(Guid itemId)
    {
        var claim = User.FindFirst(UserIdClaim)?.Value;
        if (!Guid.TryParse(claim, out var userId))
        {
            return null;
        }

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return null;
        }

        // Respects library access and parental controls of the user.
        return _libraryManager.GetItemById<BaseItem>(itemId, user);
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" or ".jpe" or ".jif" or ".jfif" or ".jfi" => MediaTypeNames.Image.Jpeg,
            ".png" => "image/png",
            ".gif" => MediaTypeNames.Image.Gif,
            ".avif" => "image/avif",
            ".bmp" or ".dib" => "image/bmp",
            ".tif" or ".tiff" => MediaTypeNames.Image.Tiff,
            _ => MediaTypeNames.Application.Octet
        };
    }
}
