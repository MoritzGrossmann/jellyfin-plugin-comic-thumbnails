namespace Jellyfin.Plugin.ComicThumbnails.Archives;

/// <summary>
/// A page (image) inside a comic archive.
/// </summary>
/// <param name="Index">Zero-based page index, as used by Jellyfin's comic reader.</param>
/// <param name="Key">Full path of the entry inside the archive.</param>
/// <param name="Name">File name of the entry, without directories.</param>
public sealed record ComicPage(int Index, string Key, string Name);
