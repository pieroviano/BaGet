using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// The Terms of Use (<c>/terms</c>) and Privacy Policy (<c>/privacy</c>) pages: the operator-supplied Markdown files
/// (<c>Site:TermsOfUsePath</c>, <c>Site:PrivacyPolicyPath</c>), 404 while a file is not configured or does not exist. The
/// first Razor Pages migrated to a controller (it replaces <c>LegalPageModel</c>, now an obsolete shim): same URLs, same
/// <c>asp-page</c> names (<see cref="LegacyPageAttribute"/>), views in <c>Views/Legal/</c>.
/// </summary>
public sealed class LegalController : UiControllerBase
{
    private readonly IOptionsSnapshot<SiteOptions> _site;
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the controller.</summary>
    public LegalController(IOptionsSnapshot<SiteOptions> site, IHostEnvironment environment)
    {
        _site = site ?? throw new ArgumentNullException(nameof(site));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>The Privacy Policy.</summary>
    [Route("privacy"), HttpGet, HttpHead, PageHandler, LegacyPage("/Privacy")]
    public Task<IActionResult> Privacy(CancellationToken cancellationToken)
        => ShowAsync(_site.Value.PrivacyPolicyPath, "Privacy Policy", cancellationToken);

    /// <summary>The Terms of Use.</summary>
    [Route("terms"), HttpGet, HttpHead, PageHandler, LegacyPage("/Terms")]
    public Task<IActionResult> Terms(CancellationToken cancellationToken)
        => ShowAsync(_site.Value.TermsOfUsePath, "Terms of Use", cancellationToken);

    private async Task<IActionResult> ShowAsync(string? path, string title, CancellationToken cancellationToken)
    {
        var content = await LegalContent.ReadAsync(path, _environment, cancellationToken);

        return content == null ? NotFound() : View(new LegalViewModel(title, content));
    }
}
