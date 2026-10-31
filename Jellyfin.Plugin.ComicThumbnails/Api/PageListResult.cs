using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ComicThumbnails.Api;

/// <summary>
/// One pager page of the page list of a comic.
/// </summary>
public sealed class PageListResult
{
    /// <summary>
    /// Gets or sets the id of the comic.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages in the comic.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the index of the first returned page.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the number of pages per request (the page size of the pager).
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the section starts collapsed.
    /// </summary>
    public bool StartCollapsed { get; set; }

    /// <summary>
    /// Gets or sets the pages.
    /// </summary>
    public IReadOnlyList<PageInfo> Pages { get; set; } = [];
}

/// <summary>
/// A page of a comic.
/// </summary>
public sealed class PageInfo
{
    /// <summary>
    /// Gets or sets the zero-based page index, as used by the reader.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the file name inside the archive.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
