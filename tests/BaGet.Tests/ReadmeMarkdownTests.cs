using BaGet.Web;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// A package's readme is written by whoever pushed it, and with open registration that
/// is anyone, so it is rendered as untrusted text.
/// </summary>
public class ReadmeMarkdownTests
{
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    public void RawHtmlIsEscapedNotRendered(string markdown)
    {
        var html = ReadmeMarkdown.ToHtml(markdown).ToString();

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<iframe", html);
        Assert.DoesNotContain("<a ", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[x][r]\n\n[r]: javascript:alert(1)")]
    public void ScriptSchemeLinksAreNotProduced(string markdown)
    {
        var html = ReadmeMarkdown.ToHtml(markdown).ToString().ToLowerInvariant();

        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("data:text/html", html);
    }

    [Fact]
    public void OrdinaryMarkdownStillRenders()
    {
        var html = ReadmeMarkdown.ToHtml("# Title\n\n**bold** [site](https://example.com)\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n```csharp\nvar x = 1;\n```").ToString();

        Assert.Contains("<h1", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains("<table", html);
        Assert.Contains("<code", html);
    }
}
