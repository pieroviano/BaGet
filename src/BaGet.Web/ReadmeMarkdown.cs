using System;
using System.Linq;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Html;

namespace BaGet.Web;

/// <summary>
/// Renders a package's readme. The package page and the upload preview use one pipeline
/// so that what an uploader is shown is what readers will be shown.
/// </summary>
public static class ReadmeMarkdown
{
    // Readmes are written by whoever pushed the package, so they are untrusted: raw HTML in
    // the source is escaped rather than rendered, and links and images with a script scheme
    // are neutralised. Markdown itself (headings, tables, code, ordinary links) is unaffected.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private static readonly string[] UnsafeSchemes = { "javascript:", "vbscript:", "data:" };

    public static HtmlString ToSafeHtml(string markdown) => ToHtml(markdown);

    public static HtmlString ToHtml(string markdown)
    {
        if (markdown == null) return null;

        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (IsUnsafe(link.Url)) link.Url = "#";
        }

        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (IsUnsafe(autolink.Url)) autolink.Url = "#";
        }

        return new HtmlString(document.ToHtml(Pipeline));
    }

    private static bool IsUnsafe(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Browsers ignore whitespace and control characters inside the scheme.
        var cleaned = new string(url.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)).ToArray());

        return UnsafeSchemes.Any(scheme => cleaned.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
    }
}