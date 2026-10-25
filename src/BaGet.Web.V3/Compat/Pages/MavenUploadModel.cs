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
/// The page model of the Maven upload page when it was a Razor Page (deploying an artifact version from the browser).
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="MavenBrowseController"/> serves <c>/maven-upload</c> (and its <c>/r/{repo}/</c> twin) and shares
/// the logic (<c>MavenUploadWorkflow</c>).
/// </remarks>
[Obsolete("The Maven upload page is served by MavenBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class MavenUploadModel : PageModel
{
    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;
    private readonly IMavenRepository _maven;
    private readonly MavenUploadWorkflow _workflow;

    public MavenUploadModel(
        IOptionsSnapshot<BaGetOptions> options,
        IOptions<AccountsOptions> accounts,
        IAuthenticationService authentication,
        IMavenRepository maven,
        IUrlGenerator urls,
        IRepositoryContext repository)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _maven = maven ?? throw new ArgumentNullException(nameof(maven));
        _workflow = new MavenUploadWorkflow(options, accounts, authentication, maven, urls, repository);
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

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl => _urls.GetMavenRepositoryUrl();

    /// <summary>The repository's name (the <c>id</c> in <c>settings.xml</c>).</summary>
    public string RepositoryName => _repository.Current.Name;

    /// <summary>The repository's policy, shown beside the form.</summary>
    public MavenRepositorySettings Settings { get; private set; } = new();

    /// <summary>What happened, for the page to show.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether <see cref="Message"/> reports a failure.</summary>
    public bool Failed { get; private set; }

    /// <summary>The artifact just deployed (<c>group:artifact</c>), so the page can link to it.</summary>
    public string? DeployedName { get; private set; }

    /// <summary>The version just deployed.</summary>
    public string? DeployedVersion { get; private set; }

    /// <summary>The coordinates just posted, so a refused form keeps them.</summary>
    public MavenDeployment? Posted { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Settings = await _maven.GetSettingsAsync(cancellationToken);
    }

    /// <summary>Deploys the chosen files under the given coordinates.</summary>
    public async Task<IActionResult> OnPostAsync(
        string? groupId,
        string? artifactId,
        string? version,
        string? packaging,
        string? classifier,
        IFormFile? file,
        IFormFile? pom,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var model = await _workflow.DeployAsync(User, HttpContext.RequestServices, groupId, artifactId, version, packaging, classifier, file, pom, apiKey, cancellationToken);
        Settings = model.Settings;
        Posted = model.Posted;
        Message = model.Message;
        Failed = model.Failed;
        DeployedName = model.DeployedName;
        DeployedVersion = model.DeployedVersion;
        if (model.StatusCode != 200) Response.StatusCode = model.StatusCode;

        return Page();
    }
}
