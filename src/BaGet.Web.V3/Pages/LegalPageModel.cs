using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// A page showing an operator-supplied Markdown file. It is a 404 while the file is not
/// configured or does not exist: BaGet ships no legal text of its own.
/// </summary>
public abstract class LegalPageModel : PageModel
{
    private readonly IHostEnvironment _environment;

    protected LegalPageModel(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public new HtmlString? Content { get; private set; }

    protected abstract string? ConfiguredPath { get; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var path = ConfiguredPath;
        if (string.IsNullOrWhiteSpace(path) || !MarkdownFilePathAttribute.IsAllowed(path)) return NotFound();

        // Path.Combine keeps an absolute path as it is.
        var fullPath = Path.Combine(_environment.ContentRootPath, path);
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        Content = ReadmeMarkdown.ToSafeHtml(await System.IO.File.ReadAllTextAsync(fullPath, cancellationToken));

        return Page();
    }
}

public class TermsModel : LegalPageModel
{
    private readonly IOptionsSnapshot<SiteOptions> _site;

    public TermsModel(IOptionsSnapshot<SiteOptions> site, IHostEnvironment environment)
        : base(environment)
    {
        _site = site ?? throw new ArgumentNullException(nameof(site));
    }

    protected override string? ConfiguredPath => _site.Value.TermsOfUsePath;
}

public class PrivacyModel : LegalPageModel
{
    private readonly IOptionsSnapshot<SiteOptions> _site;

    public PrivacyModel(IOptionsSnapshot<SiteOptions> site, IHostEnvironment environment)
        : base(environment)
    {
        _site = site ?? throw new ArgumentNullException(nameof(site));
    }

    protected override string? ConfiguredPath => _site.Value.PrivacyPolicyPath;
}
