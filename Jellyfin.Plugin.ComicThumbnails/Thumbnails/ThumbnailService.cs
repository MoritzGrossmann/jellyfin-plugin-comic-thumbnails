using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ComicThumbnails.Archives;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Drawing;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicThumbnails.Thumbnails;

/// <summary>
/// Creates page thumbnails and keeps them in Jellyfin's cache folder.
/// </summary>
public sealed class ThumbnailService : IDisposable
{
    private readonly ComicPageReader _reader;
    private readonly IImageEncoder _imageEncoder;
    private readonly ILogger<ThumbnailService> _logger;

    // Extracting and resizing is CPU and IO heavy; a detail page asks for a whole page of thumbnails at once.
    private readonly SemaphoreSlim _workers = new(Math.Max(1, Environment.ProcessorCount / 2));

    /// <summary>
    /// Initializes a new instance of the <see cref="ThumbnailService"/> class.
    /// </summary>
    /// <param name="reader">The archive reader.</param>
    /// <param name="imageEncoder">Jellyfin's image encoder.</param>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Logger.</param>
    public ThumbnailService(ComicPageReader reader, IImageEncoder imageEncoder, IApplicationPaths applicationPaths, ILogger<ThumbnailService> logger)
    {
        _reader = reader;
        _imageEncoder = imageEncoder;
        _logger = logger;
        CacheRoot = Path.Combine(applicationPaths.CachePath, "comic-thumbnails");
    }

    /// <summary>
    /// Gets the folder with all cached thumbnails.
    /// </summary>
    public string CacheRoot { get; }

    /// <summary>
    /// Returns the path of a page thumbnail and creates it if needed.
    /// </summary>
    /// <param name="item">The comic.</param>
    /// <param name="page">The page.</param>
    /// <param name="width">Maximum width in pixels.</param>
    /// <param name="quality">Encoder quality (1 to 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path of the thumbnail file.</returns>
    public async Task<string> GetThumbnailAsync(BaseItem item, ComicPage page, int width, int quality, CancellationToken cancellationToken)
    {
        var archivePath = item.Path;
        var modified = File.GetLastWriteTimeUtc(archivePath);
        var directory = GetItemDirectory(item.Id);
        var prefix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}_{3}", modified.Ticks, width, quality, page.Index);

        var existing = FindCached(directory, prefix);
        if (existing is not null)
        {
            return existing;
        }

        await _workers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another request may have created it while this one was waiting.
            existing = FindCached(directory, prefix);
            if (existing is not null)
            {
                return existing;
            }

            Directory.CreateDirectory(directory);
            RemoveStale(directory, modified);

            var sourceExtension = Path.GetExtension(page.Name).ToLowerInvariant();
            var source = Path.Combine(directory, "tmp_" + Guid.NewGuid().ToString("N") + sourceExtension);
            try
            {
                var stream = new FileStream(source, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                await using (stream.ConfigureAwait(false))
                {
                    await _reader.CopyPageToAsync(archivePath, page, stream, cancellationToken).ConfigureAwait(false);
                }

                return Encode(item, source, modified, Path.Combine(directory, prefix), sourceExtension, width, quality);
            }
            finally
            {
                TryDelete(source);
            }
        }
        finally
        {
            _workers.Release();
        }
    }

    /// <summary>
    /// Deletes all cached thumbnails.
    /// </summary>
    public void ClearCache()
    {
        if (Directory.Exists(CacheRoot))
        {
            Directory.Delete(CacheRoot, true);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _workers.Dispose();
    }

    private string Encode(BaseItem item, string source, DateTime modified, string targetWithoutExtension, string sourceExtension, int width, int quality)
    {
        if (_imageEncoder.SupportsImageEncoding)
        {
            var format = _imageEncoder.SupportedOutputFormats.Contains(ImageFormat.Webp) ? ImageFormat.Webp : ImageFormat.Jpg;
            var target = targetWithoutExtension + (format == ImageFormat.Webp ? ".webp" : ".jpg");
            try
            {
                var options = new ImageProcessingOptions
                {
                    Item = item,
                    MaxWidth = width,
                    Quality = quality,
                    SupportedOutputFormats = [format]
                };

                var result = _imageEncoder.EncodeImage(source, modified, target, true, null, quality, options, format);

                // The encoder hands back the input when the image is already small enough.
                if (!string.Equals(result, source, StringComparison.Ordinal) && File.Exists(result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resize page {Page} of {Path}, serving the original image", source, item.Path);
            }
        }

        // No encoder (or unsupported format such as some AVIF/TIFF files): keep the page as it is.
        var original = targetWithoutExtension + sourceExtension;
        File.Move(source, original, true);
        return original;
    }

    private string GetItemDirectory(Guid itemId)
    {
        return Path.Combine(CacheRoot, itemId.ToString("N", CultureInfo.InvariantCulture));
    }

    private static string? FindCached(string directory, string prefix)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, prefix + ".*").FirstOrDefault();
    }

    // Thumbnails of an older version of the file are useless once the archive changed.
    private void RemoveStale(string directory, DateTime modified)
    {
        var current = modified.Ticks.ToString(CultureInfo.InvariantCulture) + "_";
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (!name.StartsWith(current, StringComparison.Ordinal) && !name.StartsWith("tmp_", StringComparison.Ordinal))
            {
                TryDelete(file);
            }
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not delete {Path}", path);
        }
    }
}
