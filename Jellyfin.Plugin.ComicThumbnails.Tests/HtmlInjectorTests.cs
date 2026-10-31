using Jellyfin.Plugin.ComicThumbnails.Web;
using Xunit;

namespace Jellyfin.Plugin.ComicThumbnails.Tests;

public class HtmlInjectorTests
{
    private static readonly string _tag = HtmlInjector.BuildScriptTag("1.0.0.0");

    [Fact]
    public void Inject_InsertsBeforeBodyEnd()
    {
        var result = HtmlInjector.Inject("<html><body><div></div></body></html>", _tag);

        Assert.Equal("<html><body><div></div>" + _tag + "</body></html>", result);
    }

    [Fact]
    public void Inject_IsIdempotent()
    {
        var once = HtmlInjector.Inject("<body></body>", _tag);

        Assert.Equal(once, HtmlInjector.Inject(once, _tag));
    }

    [Fact]
    public void Inject_AppendsWithoutBody()
    {
        Assert.Equal("<div></div>" + _tag, HtmlInjector.Inject("<div></div>", _tag));
    }

    [Fact]
    public void BuildScriptTag_UsesRelativePathAndVersion()
    {
        Assert.Contains("src=\"../ComicThumbnails/ClientScript?v=1.0.0.0\"", _tag, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/web/", true)]
    [InlineData("/web/index.html", true)]
    [InlineData("/jellyfin/web/index.html", true)]
    [InlineData("/WEB/", true)]
    [InlineData("/web/main.js", false)]
    [InlineData("/web", false)]
    [InlineData("/Items/abc", false)]
    [InlineData(null, false)]
    public void IsIndexPath_MatchesIndexOnly(string? path, bool expected)
    {
        Assert.Equal(expected, HtmlInjector.IsIndexPath(path));
    }
}
