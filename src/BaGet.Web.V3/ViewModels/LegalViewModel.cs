using Microsoft.AspNetCore.Html;

namespace BaGet.Web;

/// <summary>What a legal page (Terms of Use, Privacy Policy) shows: its heading and the operator's Markdown, rendered safely.</summary>
/// <param name="Title">The heading and page title.</param>
/// <param name="Content">The rendered Markdown file.</param>
public sealed record LegalViewModel(string Title, HtmlString Content);
