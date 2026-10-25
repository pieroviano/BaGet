using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// The page model of the npm upload page when it was a Razor Page (publishing a <c>npm pack</c> tarball from the browser).
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="NpmBrowseController"/> serves <c>/npm-upload</c> (and its <c>/r/{repo}/</c> twin) and shares the
/// logic (<c>NpmUploadWorkflow</c>).
/// </remarks>
[Obsolete("The npm upload page is served by NpmBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class NpmUploadModel : PageModel
{
    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IUrlGenerator _urls;
    private readonly NpmUploadWorkflow _workflow;

    public NpmUploadModel(
        IOptionsSnapshot<BaGetOptions> options,
        IOptions<AccountsOptions> accounts,
        IAuthenticationService authentication,
        INpmRegistry registry,
        IUrlGenerator urls)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _workflow = new NpmUploadWorkflow(options, accounts, authentication, registry, urls);
    }

    public bool AccountsEnabled => _accounts.Value.Enabled;

    public bool IsReadOnlyMode => _options.Value.IsReadOnlyMode;

    public bool SignedIn => User.Identity?.IsAuthenticated == true;

    /// <summary>Without accounts there is no user to own an upload, so the uploader proves itself with the API key.</summary>
    public bool NeedsApiKey => !AccountsEnabled;

    /// <summary>Accounts are off and no API key is set: nothing tells one visitor from another, so the page is closed.</summary>
    public bool NothingCanAuthenticate => UploadAvailability.NothingCanAuthenticate(_options.Value, _accounts.Value);

    /// <summary>Whether to render the form: not read-only, someone could be authenticated, and (with accounts) signed in.</summary>
    public bool CanUpload => !IsReadOnlyMode && !NothingCanAuthenticate && (!AccountsEnabled || SignedIn);

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl => _urls.GetNpmRegistryUrl();

    /// <summary>The commands that publish without the browser.</summary>
    internal System.Collections.Generic.IReadOnlyList<string> PublishLines => NpmInstallSnippets.PublishLines(RegistryUrl);

    /// <summary>What happened, for the page to show.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether <see cref="Message"/> reports a failure.</summary>
    public bool Failed { get; private set; }

    /// <summary>The package just published, so the page can link to it.</summary>
    public string? PublishedName { get; private set; }

    public void OnGet()
    {
    }

    /// <summary>Publishes the chosen tarball with the dist-tag <paramref name="tag"/> (default <c>latest</c>).</summary>
    public async Task<IActionResult> OnPostAsync(IFormFile? package, string? tag, string? apiKey, CancellationToken cancellationToken)
    {
        var model = await _workflow.PublishAsync(User, HttpContext.RequestServices, package, tag, apiKey, cancellationToken);
        Message = model.Message;
        Failed = model.Failed;
        PublishedName = model.PublishedName;
        if (model.StatusCode != 200) Response.StatusCode = model.StatusCode;

        return Page();
    }
}
