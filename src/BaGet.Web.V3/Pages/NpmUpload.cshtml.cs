using System;
using System.IO;
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
/// Publishing an npm tarball (<c>npm pack</c>'s <c>.tgz</c>) from the browser. The name and version come from the
/// <c>package.json</c> inside the file; everything else is <see cref="INpmRegistry.PublishTarballAsync"/>, the call
/// <c>npm publish</c> ends in, so the page can do what the caller's API key could and no more (access model, ownership, scopes,
/// the read-only mode and the maintenance gate included). As on the NuGet upload page, an account signs in and a server without
/// accounts takes the API key typed into the form; a server that cannot tell callers apart closes the page.
/// </summary>
public class NpmUploadModel : PageModel
{
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be uploaded.";
    internal const string SignInMessage = "Sign in to upload a package.";
    internal const string ApiKeyRejectedMessage = "That API key was not accepted.";
    internal const string NoFileMessage = "Choose the .tgz file that npm pack made.";

    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IAuthenticationService _authentication;
    private readonly INpmRegistry _registry;
    private readonly IUrlGenerator _urls;

    public NpmUploadModel(
        IOptionsSnapshot<BaGetOptions> options,
        IOptions<AccountsOptions> accounts,
        IAuthenticationService authentication,
        INpmRegistry registry,
        IUrlGenerator urls)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
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
        if (IsReadOnlyMode) return Refuse(401, ReadOnlyMessage);
        if (NothingCanAuthenticate) return Refuse(401, "This server has no API key configured and no accounts, so the browser upload is closed.");

        var principal = await AuthenticateAsync(apiKey, cancellationToken);
        if (principal == null) return Refuse(401, AccountsEnabled ? SignInMessage : ApiKeyRejectedMessage);

        if (package == null || package.Length == 0) return Refuse(400, NoFileMessage);

        // The registry reads the file twice (its package.json, then the bytes), so it must be seekable.
        using var upload = package.OpenReadStream();
        using var copy = upload.CanSeek ? null : await upload.AsTemporaryFileStreamAsync(cancellationToken);
        var content = copy ?? upload;

        var chosenTag = string.IsNullOrWhiteSpace(tag) ? "latest" : tag.Trim();
        NpmResult result;
        try
        {
            result = await _registry.PublishTarballAsync(principal, content, chosenTag, cancellationToken);
        }
        catch (ScanBlockedException e)
        {
            // The scanning policy of the repository refuses a package with known vulnerabilities.
            return Refuse(403, e.Message);
        }

        if (!result.Succeeded)
        {
            return Refuse(StatusOf(result.Status), result.Message ?? "The package was not published.");
        }

        Message = result.Message ?? "Published.";
        PublishedName = result.Subject;

        return Page();
    }

    private static int StatusOf(NpmStatus status) => status switch
    {
        NpmStatus.Unauthorized => 401,
        NpmStatus.Forbidden => 403,
        NpmStatus.NotFound => 404,
        NpmStatus.Conflict => 409,
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
