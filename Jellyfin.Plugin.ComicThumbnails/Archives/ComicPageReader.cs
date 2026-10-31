using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;

namespace Jellyfin.Plugin.ComicThumbnails.Archives;

/// <summary>
/// Lists and extracts the pages of comic archives (.cbz, .cbr, .cb7, .cbt).
/// </summary>
public sealed class ComicPageReader
{
    private const int MaxCachedListings = 200;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz", ".cbr", ".cb7", ".cbt"
    };

    // Listing a large archive is cheap but not free; the pager asks for it once per page of thumbnails.
    private readonly ConcurrentDictionary<string, (DateTime Modified, IReadOnlyList<ComicPage> Pages)> _listings = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns whether the file is a comic archive this plugin can read.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <returns>True for supported comic archives.</returns>
    public static bool IsSupported(string? path)
    {
        return !string.IsNullOrEmpty(path) && _supportedExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// Returns the pages of an archive in the order of Jellyfin's comic reader.
    /// </summary>
    /// <param name="path">Path of the archive.</param>
    /// <returns>The pages.</returns>
    public IReadOnlyList<ComicPage> GetPages(string path)
    {
        var modified = File.GetLastWriteTimeUtc(path);
        if (_listings.TryGetValue(path, out var cached) && cached.Modified == modified)
        {
            return cached.Pages;
        }

        IReadOnlyList<ComicPage> pages;
        using (var archive = ArchiveFactory.Open(path))
        {
            pages = PageOrdering.Order(archive.Entries
                .Where(e => !e.IsDirectory && !string.IsNullOrEmpty(e.Key))
                .Select(e => e.Key!));
        }

        if (_listings.Count >= MaxCachedListings)
        {
            _listings.Clear();
        }

        _listings[path] = (modified, pages);
        return pages;
    }

    /// <summary>
    /// Copies the content of a page to a stream.
    /// </summary>
    /// <param name="path">Path of the archive.</param>
    /// <param name="page">The page.</param>
    /// <param name="destination">Target stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task CopyPageToAsync(string path, ComicPage page, Stream destination, CancellationToken cancellationToken)
    {
        using var archive = ArchiveFactory.Open(path);

        if (!archive.IsSolid)
        {
            var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory && string.Equals(e.Key, page.Key, StringComparison.Ordinal))
                ?? throw new FileNotFoundException($"Page {page.Key} not found in {path}");
            var source = await entry.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        // Solid archives (most .cb7 and some .cbr) can only be read front to back.
        using var reader = archive.ExtractAllEntries();
        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!reader.Entry.IsDirectory && string.Equals(reader.Entry.Key, page.Key, StringComparison.Ordinal))
            {
                var source = await reader.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (source.ConfigureAwait(false))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                return;
            }
        }

        throw new FileNotFoundException($"Page {page.Key} not found in {path}");
    }
}
