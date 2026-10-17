using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Versioning;

namespace BaGet.Web
{
    using NuGetPackageType = NuGet.Packaging.Core.PackageType;

    /// <summary>
    /// Uploading through the browser, in two steps: the first stages the file and shows what
    /// it contains, the second publishes it. Both steps go through the same authorization and
    /// the same submission pipeline as "dotnet nuget push", so the browser can do what the
    /// pusher's own API key could, and no more.
    /// </summary>
    /// <remarks>
    /// Deliberately not an AccountPageModel: that base answers 404 while accounts are
    /// disabled, and this page must still work there - with a pasted API key, exactly as the
    /// push endpoint requires one.
    /// </remarks>
    public class UploadModel : PageModel
    {
        internal const string ReadOnlyMessage =
            "This server is in read-only mode, so nothing can be uploaded.";

        internal const string SignInMessage =
            "Sign in to upload a package.";

        internal const string ApiKeyRejectedMessage =
            "That API key was not accepted.";

        internal const string NoApiKeyMessage =
            "This server has no API key configured and no accounts, so there is nothing to "
            + "tell one visitor from another and nothing to authenticate an upload with. "
            + "Set \"ApiKey\", or enable \"Accounts:Enabled\", to upload through the browser.";

        internal const string UnknownTokenMessage =
            "This upload is no longer staged. It was cancelled, already published, or staged "
            + "by someone else - upload the file again.";

        internal const string UnreadableMessage =
            "This file could not be read as a package. Upload the .nupkg or .snupkg a build "
            + "produced, not a renamed or repacked copy.";

        internal const string TermsMessage =
            "Confirm that you have the right to publish this package before submitting.";

        internal const string InvalidSymbolPackageMessage =
            "This symbol package is not valid (its entries, a PDB that is not portable, or "
            + "PDBs that do not match the package).";

        internal const string NotASymbolPackageMessage =
            "A .snupkg must declare the SymbolsPackage package type in its own nuspec. This "
            + "file does not, so it is not a symbol package - and a symbols archive is not a "
            + "package either. Repack it as a symbol package, or upload the .nupkg instead.";

        internal const string CouldNotStageMessage =
            "The server could not store this upload. Try again.";

        private readonly IOptionsSnapshot<BaGetOptions> _options;
        private readonly IOptions<AccountsOptions> _accounts;
        private readonly IAuthenticationService _authentication;
        private readonly IPackageAuthorizationService _authorization;
        private readonly IPackageSubmissionService _submissions;
        private readonly ISymbolIndexingService _symbols;
        private readonly IPackageStagingService _staging;
        private readonly IPackageDatabase _packages;
        private readonly ILogger<UploadModel> _logger;

        public UploadModel(
            IOptionsSnapshot<BaGetOptions> options,
            IOptions<AccountsOptions> accounts,
            IAuthenticationService authentication,
            IPackageAuthorizationService authorization,
            IPackageSubmissionService submissions,
            ISymbolIndexingService symbols,
            IPackageStagingService staging,
            IPackageDatabase packages,
            ILogger<UploadModel> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _submissions = submissions ?? throw new ArgumentNullException(nameof(submissions));
            _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            _staging = staging ?? throw new ArgumentNullException(nameof(staging));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// The package ID a link arrived with, as "upload a new version of this" does. It is
        /// a hint for the page only: the ID and version that are published are read from the
        /// file, never from the request.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string Id { get; set; }

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
        /// closed there rather than offered as a publish button to whoever finds the page,
        /// and the API key box - which would accept anything, including nothing - is not
        /// rendered to claim otherwise. <c>dotnet nuget push</c> is unaffected: this page is
        /// a convenience, not the authorization model.
        /// </summary>
        public bool NothingCanAuthenticate => !AccountsEnabled && string.IsNullOrEmpty(_options.Value.ApiKey);

        /// <summary>
        /// What the page says in that state, so the explanation and the refusal are one
        /// sentence rather than two that could drift apart.
        /// </summary>
        public string NoApiKeyExplanation => NoApiKeyMessage;

        /// <summary>
        /// Whether to render the form at all. Read-only mode, a server nothing can
        /// authenticate against, and an anonymous visitor each get an explanation instead of
        /// a file picker that could only fail.
        /// </summary>
        public bool CanUpload => !IsReadOnlyMode && !NothingCanAuthenticate && (!AccountsEnabled || SignedIn);

        /// <summary>
        /// The staged upload awaiting confirmation, or null while on step one.
        /// </summary>
        public StagedUpload Staged { get; private set; }

        public string Message { get; private set; }

        public bool Failed { get; private set; }

        /// <summary>
        /// Set once something is published, so the page can link to it.
        /// </summary>
        public StagedUpload Published { get; private set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostStageAsync(
            IFormFile package,
            string apiKey,
            CancellationToken cancellationToken)
        {
            if (IsReadOnlyMode) return Refuse(401, ReadOnlyMessage);
            if (NothingCanAuthenticate) return Refuse(401, NoApiKeyMessage);

            var principal = await AuthenticateAsync(apiKey, cancellationToken);
            if (principal == null) return Refuse(401, NotAuthenticatedMessage);

            if (package == null || package.Length == 0)
            {
                return Refuse(400, "Choose a .nupkg or .snupkg file to upload.");
            }

            var fileName = Path.GetFileName(package.FileName) ?? string.Empty;
            var extension = Path.GetExtension(fileName);
            if (!extension.Equals(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".snupkg", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse(400, "A package is a .nupkg file, and symbols are a .snupkg file.");
            }

            // The spooled upload is seekable, which is what reading the archive needs; one
            // that is not is copied once rather than reported as unreadable.
            using var upload = package.OpenReadStream();
            using var copy = upload.CanSeek ? null : await upload.AsTemporaryFileStreamAsync(cancellationToken);
            var content = copy ?? upload;

            var staged = await ReadOrNullAsync(content, cancellationToken);
            if (staged == null) return Refuse(400, UnreadableMessage);

            // The content decides the pipeline, never the file's name. A .snupkg whose
            // nuspec does not declare the symbols package type is therefore not a symbol
            // package - and publishing it as a package would store a symbols archive as the
            // .nupkg of that ID and version, while the symbol endpoint reads the identical
            // bytes as symbols. Refused rather than published as something it is not.
            if (extension.Equals(".snupkg", StringComparison.OrdinalIgnoreCase) && !staged.IsSymbolPackage)
            {
                return Refuse(400, NotASymbolPackageMessage);
            }

            // Authorized before the upload is stored: there is no point keeping a file its
            // uploader may not publish.
            if (!await _authorization.AuthorizeAsync(principal, PackageAction.Push, staged.Id, cancellationToken))
            {
                return Refuse(403, Denied(staged.Id));
            }

            await DescribeSymbolTargetAsync(staged, cancellationToken);

            content.Position = 0;

            try
            {
                staged.Token = await _staging.StageAsync(content, principal.UserId, cancellationToken);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // A failure the server owns, reported the way the equivalent failure on the
                // confirm step is: a sentence with a status code, never a bare 500 or a
                // stack trace.
                _logger.LogError(e, "Could not stage an upload of {PackageId} {PackageVersion}", staged.Id, staged.Version);

                return Refuse(500, CouldNotStageMessage);
            }

            Staged = staged;

            return Page();
        }

        public async Task<IActionResult> OnPostSubmitAsync(
            string token,
            string apiKey,
            bool acceptTerms,
            CancellationToken cancellationToken)
        {
            if (IsReadOnlyMode) return Refuse(401, ReadOnlyMessage);
            if (NothingCanAuthenticate) return Refuse(401, NoApiKeyMessage);

            var principal = await AuthenticateAsync(apiKey, cancellationToken);
            if (principal == null) return Refuse(401, NotAuthenticatedMessage);

            // Whoever staged the upload is the only one who can open it, so another user's
            // token is indistinguishable from one that never existed.
            using var stagedStream = await _staging.OpenAsync(token, principal.UserId, cancellationToken);
            if (stagedStream == null) return Refuse(404, UnknownTokenMessage);

            using var content = await stagedStream.AsTemporaryFileStreamAsync(cancellationToken);

            var staged = await ReadOrNullAsync(content, cancellationToken);
            if (staged == null)
            {
                await _staging.RemoveAsync(token, principal.UserId, cancellationToken);
                return Refuse(400, UnreadableMessage);
            }

            staged.Token = token;
            await DescribeSymbolTargetAsync(staged, cancellationToken);

            // The checkbox is enforced here too, not only in the browser: the upload stays
            // staged so the box can be ticked and the preview submitted again.
            if (!acceptTerms)
            {
                Staged = staged;
                return Refuse(400, TermsMessage);
            }

            // Re-checked at submission: ownership may have changed while the preview was on
            // screen, and the first check guarded the store, not the publish.
            if (!await _authorization.AuthorizeAsync(principal, PackageAction.Push, staged.Id, cancellationToken))
            {
                await _staging.RemoveAsync(token, principal.UserId, cancellationToken);
                return Refuse(403, Denied(staged.Id));
            }

            content.Position = 0;

            return staged.IsSymbolPackage
                ? await PublishSymbolsAsync(staged, content, principal, cancellationToken)
                : await PublishPackageAsync(staged, content, principal, cancellationToken);
        }

        /// <summary>
        /// Discard a staged upload. Authenticated, owner-checked and refused in read-only
        /// mode, exactly as the other two handlers are: the token is the only handle on an
        /// upload, so anyone who learned one - from a log, a screenshot, a shared screen -
        /// could otherwise make its owner upload the whole file again, and the deletion is a
        /// write to the store that read-only mode is meant to freeze.
        /// </summary>
        public async Task<IActionResult> OnPostCancelAsync(
            string token,
            string apiKey,
            CancellationToken cancellationToken)
        {
            if (IsReadOnlyMode) return Refuse(401, ReadOnlyMessage);
            if (NothingCanAuthenticate) return Refuse(401, NoApiKeyMessage);

            var principal = await AuthenticateAsync(apiKey, cancellationToken);
            if (principal == null) return Refuse(401, NotAuthenticatedMessage);

            // Another user's token is indistinguishable from one that never existed, as it
            // is when submitting.
            if (!await _staging.RemoveAsync(token, principal.UserId, cancellationToken))
            {
                return Refuse(404, UnknownTokenMessage);
            }

            Message = "The upload was discarded.";

            return Page();
        }

        private async Task<IActionResult> PublishPackageAsync(
            StagedUpload staged,
            Stream content,
            BaGetPrincipal principal,
            CancellationToken cancellationToken)
        {
            PackageIndexingResult result;
            try
            {
                result = await _submissions.SubmitAsync(content, cancellationToken);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // A failure the server owns: said in a sentence rather than as a stack trace,
                // and the upload is kept so it can be submitted again.
                _logger.LogError(e, "Could not publish the staged upload {Token}", staged.Token);

                Staged = staged;
                return Refuse(500, "The server could not publish this package. Try again.");
            }

            // An accepted package claims its ID now, as it does on the push endpoint: it is
            // the pusher's even while it is being validated.
            if (result == PackageIndexingResult.Success || result == PackageIndexingResult.Accepted)
            {
                await _authorization.OnPushedAsync(principal, staged.Id, cancellationToken);
            }

            await _staging.RemoveAsync(staged.Token, principal.UserId, cancellationToken);

            switch (result)
            {
                case PackageIndexingResult.Success:
                case PackageIndexingResult.Accepted:
                    Published = staged;
                    Message = result == PackageIndexingResult.Accepted
                        ? $"{staged.Id} {staged.Version} was accepted and is being validated."
                        : $"{staged.Id} {staged.Version} was published.";
                    return Page();

                case PackageIndexingResult.PackageAlreadyExists:
                    // This upload can never be published, so keeping it staged would leave a
                    // file nobody could use.
                    return Refuse(409,
                        $"{staged.Id} {staged.Version} is already on this server, and this server does "
                        + "not replace a published version. Publish a new version instead.");

                default:
                    return Refuse(400, UnreadableMessage);
            }
        }

        private async Task<IActionResult> PublishSymbolsAsync(
            StagedUpload staged,
            Stream content,
            BaGetPrincipal principal,
            CancellationToken cancellationToken)
        {
            // Branched on the indexing result, not on a status code: a malformed symbol
            // package and one whose package is missing are told apart here, while the push
            // endpoint reports the first as 400 and every exception as 400 as well.
            var result = await _symbols.IndexAsync(content, cancellationToken);

            await _staging.RemoveAsync(staged.Token, principal.UserId, cancellationToken);

            switch (result)
            {
                case SymbolIndexingResult.Success:
                    // No OnPushedAsync: symbols never claim an ID, exactly as on the symbol
                    // endpoint, so a symbol upload confers no ownership.
                    Published = staged;
                    Message = $"The symbols for {staged.Id} {staged.Version} were published.";
                    return Page();

                case SymbolIndexingResult.PackageNotFound:
                    return Refuse(404, MissingSymbolTarget(staged));

                default:
                    return Refuse(400, InvalidSymbolPackageMessage);
            }
        }

        /// <summary>
        /// Who is uploading. With accounts on it is the signed-in session; with accounts off
        /// there is no session to trust, so a pasted API key authenticates instead.
        /// </summary>
        private async Task<BaGetPrincipal> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (!AccountsEnabled)
            {
                return await _authentication.AuthenticatePrincipalAsync(apiKey, cancellationToken);
            }

            // Resolved per request: the factory is only registered while accounts are on.
            return HttpContext.RequestServices.GetService<SessionPrincipalFactory>()?.Create(User);
        }

        private string NotAuthenticatedMessage => AccountsEnabled ? SignInMessage : ApiKeyRejectedMessage;

        private static string Denied(string packageId)
            => $"You may not publish '{packageId}'. Either someone else owns it, or it predates "
                + "this server's accounts and only an administrator may publish to it.";

        private static string MissingSymbolTarget(StagedUpload staged)
            => "Symbols can only be uploaded for a package version already on this server. "
                + $"Push {staged.Id} {staged.Version} first.";

        /// <summary>
        /// Everything the preview shows, read from the archive itself. Null when the file is
        /// not a package at all.
        /// </summary>
        private async Task<StagedUpload> ReadOrNullAsync(Stream content, CancellationToken cancellationToken)
        {
            try
            {
                content.Position = 0;

                using var reader = new PackageArchiveReader(content, leaveStreamOpen: true);
                var nuspec = reader.NuspecReader;

                // The content decides which pipeline runs, not the file's name: a symbol
                // package says so in its own nuspec, so renaming a file cannot send it to a
                // pipeline its content does not belong to.
                var isSymbolPackage = nuspec
                    .GetPackageTypes()
                    .Any(t => string.Equals(t.Name, NuGetPackageType.SymbolsPackage.Name, StringComparison.OrdinalIgnoreCase));

                var staged = new StagedUpload
                {
                    IsSymbolPackage = isSymbolPackage,
                    Id = nuspec.GetId(),
                    Version = nuspec.GetVersion().ToNormalizedString(),
                };

                if (isSymbolPackage)
                {
                    staged.SymbolFiles = reader
                        .GetFiles()
                        .Where(f => Path.GetExtension(f).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return staged;
                }

                var metadata = reader.GetPackageMetadata();

                staged.Description = metadata.Description;
                staged.Authors = metadata.Authors ?? Array.Empty<string>();
                staged.LicenseUrl = metadata.LicenseUrlString;
                staged.License = nuspec.GetLicenseMetadata()?.License;
                staged.DependencyGroups = PackageDependencies.GroupByFramework(metadata.Dependencies);

                if (reader.HasReadme())
                {
                    using var readme = await reader.GetReadmeAsync(cancellationToken);
                    using var text = new StreamReader(readme);

                    staged.Readme = ReadmeMarkdown.ToHtml(await text.ReadToEndAsync(cancellationToken));
                }

                return staged;
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _logger.LogInformation(e, "An upload could not be read as a package");
                return null;
            }
            finally
            {
                if (content.CanSeek) content.Position = 0;
            }
        }

        /// <summary>
        /// Symbols carry no metadata of their own, so the preview reports the version they
        /// belong to - and says now, rather than after the upload is confirmed, when there is
        /// no such version to attach them to.
        /// </summary>
        private async Task DescribeSymbolTargetAsync(StagedUpload staged, CancellationToken cancellationToken)
        {
            if (!staged.IsSymbolPackage) return;
            if (!NuGetVersion.TryParse(staged.Version, out var version)) return;

            var target = await _packages.FindOrNullAsync(staged.Id, version, includeUnlisted: true, cancellationToken);

            staged.TargetPackageExists = target != null;
            staged.TargetPackageMessage = target == null ? MissingSymbolTarget(staged) : null;
        }

        private PageResult Refuse(int statusCode, string message)
        {
            Failed = true;
            Message = message;

            var page = Page();
            page.StatusCode = statusCode;

            return page;
        }

        /// <summary>
        /// What a staged upload contains, as the preview shows it.
        /// </summary>
        public class StagedUpload
        {
            public string Token { get; set; }

            public bool IsSymbolPackage { get; set; }

            public string Id { get; set; }

            public string Version { get; set; }

            public string Description { get; set; }

            public IReadOnlyList<string> Authors { get; set; } = Array.Empty<string>();

            /// <summary>
            /// The license expression or license file the package declares, if any.
            /// </summary>
            public string License { get; set; }

            public string LicenseUrl { get; set; }

            public IReadOnlyList<DependencyGroupModel> DependencyGroups { get; set; }
                = Array.Empty<DependencyGroupModel>();

            public HtmlString Readme { get; set; }

            public IReadOnlyList<string> SymbolFiles { get; set; } = Array.Empty<string>();

            public bool TargetPackageExists { get; set; }

            public string TargetPackageMessage { get; set; }
        }
    }
}
