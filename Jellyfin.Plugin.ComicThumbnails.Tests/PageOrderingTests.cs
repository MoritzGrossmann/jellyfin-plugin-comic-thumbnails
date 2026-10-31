using System.Linq;
using Jellyfin.Plugin.ComicThumbnails.Archives;
using Xunit;

namespace Jellyfin.Plugin.ComicThumbnails.Tests;

public class PageOrderingTests
{
    [Theory]
    [InlineData("page01.jpg", true)]
    [InlineData("PAGE01.JPG", true)]
    [InlineData("cover.webp", true)]
    [InlineData("scan.jfif", true)]
    [InlineData("scan.tiff", true)]
    [InlineData("page.avif", true)]
    [InlineData("ComicInfo.xml", false)]
    [InlineData("readme.txt", false)]
    [InlineData("noextension", false)]
    [InlineData("archive.jpg.zip", false)]
    public void IsPage_MatchesWebReaderFormats(string name, bool expected)
    {
        Assert.Equal(expected, PageOrdering.IsPage(name));
    }

    [Fact]
    public void Order_SortsByFileNameOrdinal_LikeJavaScript()
    {
        // JavaScript's "<" compares UTF-16 code units: upper case before lower case, "10" before "2".
        var pages = PageOrdering.Order(["b.jpg", "a.jpg", "B.jpg", "page10.jpg", "page2.jpg", "ComicInfo.xml"]);

        Assert.Equal(["B.jpg", "a.jpg", "b.jpg", "page10.jpg", "page2.jpg"], pages.Select(p => p.Name));
        Assert.Equal([0, 1, 2, 3, 4], pages.Select(p => p.Index));
    }

    [Fact]
    public void Order_IgnoresDirectories_WhenSorting()
    {
        // The reader sorts by file name only, so the folder does not matter.
        var pages = PageOrdering.Order(["z/001.png", "a/002.png", "000.png"]);

        Assert.Equal(["000.png", "z/001.png", "a/002.png"], pages.Select(p => p.Key));
    }

    [Fact]
    public void Order_IsStable_ForEqualNames()
    {
        var pages = PageOrdering.Order(["b/01.jpg", "a/01.jpg"]);

        Assert.Equal(["b/01.jpg", "a/01.jpg"], pages.Select(p => p.Key));
    }

    [Theory]
    [InlineData("a/b/c.jpg", "c.jpg")]
    [InlineData("a\\b\\c.jpg", "c.jpg")]
    [InlineData("c.jpg", "c.jpg")]
    public void GetFileName_StripsDirectories(string key, string expected)
    {
        Assert.Equal(expected, PageOrdering.GetFileName(key));
    }
}
