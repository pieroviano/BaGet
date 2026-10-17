using Markdig;
using Microsoft.AspNetCore.Html;

namespace BaGet.Web
{
    /// <summary>
    /// Renders a package's readme. The package page and the upload preview use one pipeline
    /// so that what an uploader is shown is what readers will be shown.
    /// </summary>
    public static class ReadmeMarkdown
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static HtmlString ToHtml(string markdown)
            => markdown == null ? null : new HtmlString(Markdown.ToHtml(markdown, Pipeline));
    }
}
