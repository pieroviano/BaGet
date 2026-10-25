using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// The page model of the NuGet upload page when it was a Razor Page: uploading through the browser in two steps, the first stages
/// the file and shows what it contains, the second publishes it.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="UploadController"/> serves <c>/upload</c> (and <c>/r/{repo}/upload</c>) and shares the logic
/// (<c>UploadWorkflow</c>, which keeps the rules: the same authorization and submission pipeline as <c>dotnet nuget push</c>, and a
/// pasted API key where accounts are off).
/// </remarks>
[Obsolete("The upload page is served by UploadController; this page model has no page any more and will be removed in the next major version.")]
public class UploadModel : PageModel
{
    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly UploadWorkflow _workflow;

    public UploadModel(
        IOptionsSnapshot<BaGetOptions> options,
        IOptions<AccountsOptions> accounts,
        IAuthenticationService authentication,
        IPackageAuthorizationService authorization,
        IPackageSubmissionService submissions,
        ISymbolIndexingService symbols,
        IPackageStagingService staging,
        IPackageDatabase packages,
        ILogger<UploadModel> logger,
        IScanGate scanGate)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _workflow = new UploadWorkflow(options, accounts, authentication, authorization, submissions, symbols, staging, packages, logger, scanGate);
    }

    /// <summary>
    /// The package ID a link arrived with, as "upload a new version of this" does. It is
    /// a hint for the page only: the ID and version that are published are read from the
    /// file, never from the request.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    public bool AccountsEnabled => _accounts.Value.Enabled;

    public bool IsReadOnlyMode => _options.Value.IsReadOnlyMode;

    public bool SignedIn => User.Identity?.IsAuthenticated == true;

    /// <summary>
    /// With accounts off there is no user to own an upload, so the pusher proves itself
    /// with an API key - the same key the PUT endpoint requires.
    /// </summary>
    public bool NeedsApiKey => !AccountsEnabled;

    /// <summary>
    /// Nothing on this server can tell one caller from another: accounts are off and no
    /// global API key is configured, which is how BaGet ships. The browser upload is
    /// closed there rather than offered as a publish button to whoever finds the page.
    /// </summary>
    public bool NothingCanAuthenticate => UploadAvailability.NothingCanAuthenticate(_options.Value, _accounts.Value);

    /// <summary>
    /// What the page says in that state, so the explanation and the refusal are one
    /// sentence rather than two that could drift apart.
    /// </summary>
    public string NoApiKeyExplanation => UploadWorkflow.NoApiKeyMessage;

    /// <summary>
    /// Whether to render the form at all. Read-only mode, a server nothing can
    /// authenticate against, and an anonymous visitor each get an explanation instead of
    /// a file picker that could only fail.
    /// </summary>
    public bool CanUpload => !IsReadOnlyMode && !NothingCanAuthenticate && (!AccountsEnabled || SignedIn);

    /// <summary>
    /// The staged upload awaiting confirmation, or null while on step one.
    /// </summary>
    public StagedUpload? Staged { get; private set; }

    public string? Message { get; private set; }

    public bool Failed { get; private set; }

    /// <summary>
    /// Set once something is published, so the page can link to it.
    /// </summary>
    public StagedUpload? Published { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostStageAsync(
        IFormFile? package,
        string? apiKey,
        CancellationToken cancellationToken)
        => Show(await _workflow.StageAsync(User, HttpContext.RequestServices, Id, package, apiKey, cancellationToken));

    public async Task<IActionResult> OnPostSubmitAsync(
        string token,
        string? apiKey,
        bool acceptTerms,
        CancellationToken cancellationToken)
        => Show(await _workflow.SubmitAsync(User, HttpContext.RequestServices, Id, token, apiKey, acceptTerms, cancellationToken));

    /// <summary>
    /// Discard a staged upload. Authenticated, owner-checked and refused in read-only
    /// mode, exactly as the other two handlers are.
    /// </summary>
    public async Task<IActionResult> OnPostCancelAsync(
        string token,
        string? apiKey,
        CancellationToken cancellationToken)
        => Show(await _workflow.CancelAsync(User, HttpContext.RequestServices, Id, token, apiKey, cancellationToken));

    /// <summary>
    /// The token of a staged upload whose API key was rejected, so the key can be entered
    /// again. Nothing about the upload is shown: it was not authenticated.
    /// </summary>
    public string? RetryToken { get; private set; }

    private IActionResult Show(UploadViewModel model)
    {
        Staged = model.Staged == null ? null : StagedUpload.From(model.Staged);
        Published = model.Published == null ? null : StagedUpload.From(model.Published);
        Message = model.Message;
        Failed = model.Failed;
        RetryToken = model.RetryToken;

        var page = Page();
        page.StatusCode = model.StatusCode;

        return page;
    }

    /// <summary>
    /// What a staged upload contains, as the preview shows it.
    /// </summary>
    public class StagedUpload
    {
        public string? Token { get; set; }

        public bool IsSymbolPackage { get; set; }

        public string? Id { get; set; }

        public string? Version { get; set; }

        public string? Description { get; set; }

        public IReadOnlyList<string> Authors { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The license expression or license file the package declares, if any.
        /// </summary>
        public string? License { get; set; }

        public string? LicenseUrl { get; set; }

        public IReadOnlyList<DependencyGroupModel> DependencyGroups { get; set; }
            = Array.Empty<DependencyGroupModel>();

        public HtmlString? Readme { get; set; }

        public IReadOnlyList<string> SymbolFiles { get; set; } = Array.Empty<string>();

        public bool TargetPackageExists { get; set; }

        public string? TargetPackageMessage { get; set; }

        internal static StagedUpload From(UploadPreview preview) => new()
        {
            Token = preview.Token,
            IsSymbolPackage = preview.IsSymbolPackage,
            Id = preview.Id,
            Version = preview.Version,
            Description = preview.Description,
            Authors = preview.Authors,
            License = preview.License,
            LicenseUrl = preview.LicenseUrl,
            DependencyGroups = preview.DependencyGroups,
            Readme = preview.Readme,
            SymbolFiles = preview.SymbolFiles,
            TargetPackageExists = preview.TargetPackageExists,
            TargetPackageMessage = preview.TargetPackageMessage,
        };
    }
}
