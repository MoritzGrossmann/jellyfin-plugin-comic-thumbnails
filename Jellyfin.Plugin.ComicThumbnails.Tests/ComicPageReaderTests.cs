using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ComicThumbnails.Archives;
using Xunit;

namespace Jellyfin.Plugin.ComicThumbnails.Tests;

public sealed class ComicPageReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "comic-thumbnails-tests-" + Guid.NewGuid().ToString("N"));

    public ComicPageReaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Theory]
    [InlineData("a.cbz", true)]
    [InlineData("a.CBR", true)]
    [InlineData("a.cb7", true)]
    [InlineData("a.cbt", true)]
    [InlineData("a.epub", false)]
    [InlineData("a.pdf", false)]
    [InlineData(null, false)]
    public void IsSupported_ChecksExtension(string? path, bool expected)
    {
        Assert.Equal(expected, ComicPageReader.IsSupported(path));
    }

    [Fact]
    public void GetPages_ListsImagesInReaderOrder()
    {
        var path = CreateCbz("comic.cbz", "ComicInfo.xml", "images/p02.jpg", "images/p01.jpg", "images/", "p10.png");

        var pages = new ComicPageReader().GetPages(path);

        Assert.Equal(["p01.jpg", "p02.jpg", "p10.png"], pages.Select(p => p.Name));
    }

    [Fact]
    public async Task CopyPageToAsync_ReturnsTheEntryContent()
    {
        var path = CreateCbz("comic.cbz", "001.jpg", "002.jpg");
        var reader = new ComicPageReader();
        var page = reader.GetPages(path)[1];

        using var output = new MemoryStream();
        await reader.CopyPageToAsync(path, page, output, CancellationToken.None);

        Assert.Equal("002.jpg", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public void GetPages_RereadsChangedArchives()
    {
        var reader = new ComicPageReader();
        var path = CreateCbz("comic.cbz", "001.jpg");
        Assert.Single(reader.GetPages(path));

        File.Delete(path);
        CreateCbz("comic.cbz", "001.jpg", "002.jpg");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(2, reader.GetPages(path).Count);
    }

    // Each file entry contains its own name, so the content identifies the page.
    private string CreateCbz(string name, params string[] entries)
    {
        var path = Path.Combine(_directory, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            var zipEntry = zip.CreateEntry(entry);
            if (!entry.EndsWith('/'))
            {
                using var stream = zipEntry.Open();
                stream.Write(Encoding.UTF8.GetBytes(entry));
            }
        }

        return path;
    }
}
