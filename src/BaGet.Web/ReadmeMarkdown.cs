using Markdig;
using Microsoft.AspNetCore.Html;

namespace BaGet.Web;

/// <summary>
/// Renders a package's readme. The package page and the upload preview use one pipeline
/// so that what an uploader is shown is what readers will be shown.
/// </summary>
public static class ReadmeMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // For text that is not a package's: raw HTML in the source is escaped, and links with
    // a script scheme are not produced, so a file an operator edits cannot inject markup.
    private static readonly MarkdownPipeline SafePipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static HtmlString ToSafeHtml(string markdown)
        => markdown == null ? null : new HtmlString(Markdown.ToHtml(markdown, SafePipeline));

    public static HtmlString ToHtml(string markdown)
        => markdown == null ? null : new HtmlString(Markdown.ToHtml(markdown, Pipeline));
}