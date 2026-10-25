using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// The page model of the Terms of Use and Privacy Policy pages when they were Razor Pages. A page showing an operator-supplied
/// Markdown file: a 404 while the file is not configured or does not exist (BaGet ships no legal text of its own).
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="LegalController"/> serves <c>/terms</c> and <c>/privacy</c>. Both share <c>LegalContent</c>.
/// </remarks>
[Obsolete("The legal pages are served by LegalController; this page model has no page any more and will be removed in the next major version.")]
public abstract class LegalPageModel : PageModel
{
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the page model.</summary>
    protected LegalPageModel(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>The rendered Markdown, set by <see cref="OnGetAsync"/>.</summary>
    public new HtmlString? Content { get; private set; }

    /// <summary>The configured Markdown file path.</summary>
    protected abstract string? ConfiguredPath { get; }

    /// <summary>Reads the file; a 404 when it is not configured or does not exist.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Content = await LegalContent.ReadAsync(ConfiguredPath, _environment, cancellationToken);

        return Content == null ? NotFound() : Page();
    }
}

/// <summary>The Terms of Use page model (see <see cref="LegalPageModel"/>).</summary>
[Obsolete("The legal pages are served by LegalController; this page model has no page any more and will be removed in the next major version.")]
public class TermsModel : LegalPageModel
{
    private readonly IOptionsSnapshot<SiteOptions> _site;

    /// <summary>Creates the page model.</summary>
    public TermsModel(IOptionsSnapshot<SiteOptions> site, IHostEnvironment environment)
        : base(environment)
    {
        _site = site ?? throw new ArgumentNullException(nameof(site));
    }

    /// <inheritdoc/>
    protected override string? ConfiguredPath => _site.Value.TermsOfUsePath;
}

/// <summary>The Privacy Policy page model (see <see cref="LegalPageModel"/>).</summary>
[Obsolete("The legal pages are served by LegalController; this page model has no page any more and will be removed in the next major version.")]
public class PrivacyModel : LegalPageModel
{
    private readonly IOptionsSnapshot<SiteOptions> _site;

    /// <summary>Creates the page model.</summary>
    public PrivacyModel(IOptionsSnapshot<SiteOptions> site, IHostEnvironment environment)
        : base(environment)
    {
        _site = site ?? throw new ArgumentNullException(nameof(site));
    }

    /// <inheritdoc/>
    protected override string? ConfiguredPath => _site.Value.PrivacyPolicyPath;
}
