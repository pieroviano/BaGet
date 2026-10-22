using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// Deploying an artifact version from the browser: the coordinates, the main file with an optional classifier, and a POM (made
/// from the coordinates when none is given). Everything is <see cref="IMavenRepository.DeployAsync"/>, the call
/// <c>mvn deploy</c>'s uploads end in, so the page can do what the caller's API key could and no more (access model, ownership,
/// scopes, the repository's release/snapshot policy and redeploy rule, the read-only mode and the maintenance gate included). As
/// on the NuGet upload page, an account signs in and a server without accounts takes the API key typed into the form; a server
/// that cannot tell callers apart closes the page.
/// </summary>
public class MavenUploadModel : PageModel
{
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be uploaded.";
    internal const string SignInMessage = "Sign in to upload an artifact.";
    internal const string ApiKeyRejectedMessage = "That API key was not accepted.";
    internal const string NoFileMessage = "Choose a file or a POM to deploy.";

    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IAuthenticationService _authentication;
    private readonly IMavenRepository _maven;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

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
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _maven = maven ?? throw new ArgumentNullException(nameof(maven));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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
        Settings = await _maven.GetSettingsAsync(cancellationToken);
        Posted = new MavenDeployment { GroupId = groupId, ArtifactId = artifactId, Version = version, Packaging = packaging, Classifier = classifier };

        if (IsReadOnlyMode) return Refuse(401, ReadOnlyMessage);
        if (NothingCanAuthenticate) return Refuse(401, "This server has no API key configured and no accounts, so the browser upload is closed.");

        var principal = await AuthenticateAsync(apiKey, cancellationToken);
        if (principal == null) return Refuse(401, AccountsEnabled ? SignInMessage : ApiKeyRejectedMessage);

        var hasFile = file != null && file.Length > 0;
        var hasPom = pom != null && pom.Length > 0;
        if (!hasFile && !hasPom) return Refuse(400, NoFileMessage);

        await using var main = hasFile ? file!.OpenReadStream() : null;
        await using var pomStream = hasPom ? pom!.OpenReadStream() : null;
        var result = await _maven.DeployAsync(
            principal,
            new MavenDeployment
            {
                GroupId = groupId,
                ArtifactId = artifactId,
                Version = version,
                Packaging = packaging,
                Classifier = classifier,
                File = main,
                Pom = pomStream,
            },
            cancellationToken);
        if (!result.Succeeded)
        {
            return Refuse(StatusOf(result.Status), result.Message ?? "The artifact was not deployed.");
        }

        Message = result.Message ?? "Deployed.";
        DeployedName = result.Subject;
        DeployedVersion = version?.Trim();

        return Page();
    }

    private static int StatusOf(MavenStatus status) => status switch
    {
        MavenStatus.Unauthorized => 401,
        MavenStatus.Forbidden => 403,
        MavenStatus.NotFound => 404,
        MavenStatus.Conflict => 409,
        MavenStatus.TooLarge => 413,
        _ => 400,
    };

    private IActionResult Refuse(int status, string message)
    {
        Message = message;
        Failed = true;
        Response.StatusCode = status;

        return Page();
    }

    private async Task<BaGetPrincipal?> AuthenticateAsync(string? apiKey, CancellationToken cancellationToken)
    {
        if (!AccountsEnabled) return await _authentication.AuthenticatePrincipalAsync(apiKey, cancellationToken);

        // Resolved per request: the factory is only registered while accounts are on.
        var factory = HttpContext.RequestServices.GetService<SessionPrincipalFactory>();

        return factory == null ? null : await factory.CreateAsync(User);
    }
}
