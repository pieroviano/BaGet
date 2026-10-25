using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// The logic of the npm front page: the packages whose name contains the text, listed only when the visitor may open their pages.
/// Used by <c>NpmBrowseController</c>.
/// </summary>
internal sealed class NpmPackagesWorkflow
{
    /// <summary>Packages per page.</summary>
    public const int ResultsPerPage = 20;

    private readonly INpmRegistry _registry;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryAccess _access;
    private readonly IRepositoryContext _repository;

    public NpmPackagesWorkflow(INpmRegistry registry, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<NpmPackagesViewModel> LoadAsync(string? query, int pageIndex, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        // The listing names only the packages whose pages the visitor may open.
        var repository = _repository.Current;
        var result = await _registry.SearchAsync(
            query,
            (pageIndex - 1) * ResultsPerPage,
            ResultsPerPage,
            (name, token) => _access.CanReadPackageAsync(user, repository, name, token),
            cancellationToken);

        return new NpmPackagesViewModel { Query = query, PageIndex = pageIndex, Result = result, RegistryUrl = _urls.GetNpmRegistryUrl() };
    }
}

/// <summary>
/// The logic of the page of one npm package: its readme, versions, dist-tags, owners and install commands. Shared by
/// <c>NpmBrowseController</c>.
/// </summary>
internal sealed class NpmPackageWorkflow
{
    private readonly INpmRegistry _registry;
    private readonly IUrlGenerator _urls;

    public NpmPackageWorkflow(INpmRegistry registry, IUrlGenerator urls)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
    }

    /// <summary>The page's data; null when the name is not a valid npm name (the page answers a plain 404).</summary>
    public async Task<NpmPackageViewModel?> LoadAsync(string? id, string? requestedVersion, ClaimsPrincipal user, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!NpmNames.TryParseLookup(id, out var name, out _)) return null;

        var registryUrl = _urls.GetNpmRegistryUrl();
        var package = await _registry.GetPackageAsync(name, cancellationToken);
        if (package == null)
        {
            // The page explains what is missing, with the status a link checker expects.
            return new NpmPackageViewModel { Id = id, RequestedVersion = requestedVersion, RegistryUrl = registryUrl, StatusCode = 404 };
        }

        var selected = (requestedVersion == null ? null : package.Versions.FirstOrDefault(v => v.Version == requestedVersion))
                       ?? package.Latest;
        if (selected == null)
        {
            return new NpmPackageViewModel { Id = id, RequestedVersion = requestedVersion, RegistryUrl = registryUrl, Package = package };
        }

        var readme = ReadmeMarkdown.ToHtml(selected.Readme);
        var selectedTags = package.DistTags.Where(t => t.Value == selected.Version).Select(t => t.Key).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var installTabs = NpmInstallSnippets.Build(package.Name, selected.Version, selectedTags.FirstOrDefault(t => t != "latest"), registryUrl);

        // Owners and the manage page belong to a repository that holds packages of its own; a remote one caches an upstream's and a
        // virtual one holds none (it is managed where the package is stored).
        IReadOnlyList<string> owners = Array.Empty<string>();
        var canManage = false;
        var kind = services.GetService<IRepositoryContext>()?.Current.Kind ?? RepositoryKind.Local;
        if (kind == RepositoryKind.Local && services.GetService<IOptions<AccountsOptions>>()?.Value.Enabled == true)
        {
            var ownership = services.GetService<PackageOwnershipService>();
            if (ownership != null)
            {
                owners = (await ownership.GetOwnersAsync(package.Name, cancellationToken)).Select(o => o.OwnerName ?? string.Empty).ToList();
                canManage = await NpmManageAccess.CanOpenAsync(services, user, package.Name, cancellationToken);
            }
        }

        return new NpmPackageViewModel
        {
            Id = id,
            RequestedVersion = requestedVersion,
            RegistryUrl = registryUrl,
            Package = package,
            Selected = selected,
            Readme = readme,
            SelectedTags = selectedTags,
            InstallTabs = installTabs,
            Owners = owners,
            CanManage = canManage,
        };
    }
}

/// <summary>
/// The logic of the npm upload page: publishing a tarball through <see cref="INpmRegistry.PublishTarballAsync"/>, the call
/// <c>npm publish</c> ends in. Used by <c>NpmBrowseController</c>.
/// </summary>
internal sealed class NpmUploadWorkflow
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

    public NpmUploadWorkflow(
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

    /// <summary>The page before anything was posted.</summary>
    public NpmUploadViewModel Show(ClaimsPrincipal user) => Build(user, null, false, null, 200);

    /// <summary>Publishes the chosen tarball with the dist-tag <paramref name="tag"/> (default <c>latest</c>).</summary>
    public async Task<NpmUploadViewModel> PublishAsync(
        ClaimsPrincipal user,
        IServiceProvider services,
        IFormFile? package,
        string? tag,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (_options.Value.IsReadOnlyMode) return Refuse(user, 401, ReadOnlyMessage);
        if (UploadAvailability.NothingCanAuthenticate(_options.Value, _accounts.Value))
        {
            return Refuse(user, 401, "This server has no API key configured and no accounts, so the browser upload is closed.");
        }

        var principal = await AuthenticateAsync(user, services, apiKey, cancellationToken);
        if (principal == null) return Refuse(user, 401, _accounts.Value.Enabled ? SignInMessage : ApiKeyRejectedMessage);

        if (package == null || package.Length == 0) return Refuse(user, 400, NoFileMessage);

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
            return Refuse(user, 403, e.Message);
        }

        if (!result.Succeeded)
        {
            return Refuse(user, StatusOf(result.Status), result.Message ?? "The package was not published.");
        }

        return Build(user, result.Message ?? "Published.", false, result.Subject, 200);
    }

    private static int StatusOf(NpmStatus status) => status switch
    {
        NpmStatus.Unauthorized => 401,
        NpmStatus.Forbidden => 403,
        NpmStatus.NotFound => 404,
        NpmStatus.Conflict => 409,
        _ => 400,
    };

    private NpmUploadViewModel Refuse(ClaimsPrincipal user, int status, string message) => Build(user, message, true, null, status);

    private NpmUploadViewModel Build(ClaimsPrincipal user, string? message, bool failed, string? publishedName, int status)
    {
        var registryUrl = _urls.GetNpmRegistryUrl();

        return new NpmUploadViewModel
        {
            AccountsEnabled = _accounts.Value.Enabled,
            IsReadOnlyMode = _options.Value.IsReadOnlyMode,
            SignedIn = user.Identity?.IsAuthenticated == true,
            NothingCanAuthenticate = UploadAvailability.NothingCanAuthenticate(_options.Value, _accounts.Value),
            RegistryUrl = registryUrl,
            PublishLines = NpmInstallSnippets.PublishLines(registryUrl),
            Message = message,
            Failed = failed,
            PublishedName = publishedName,
            StatusCode = status,
        };
    }

    private async Task<BaGetPrincipal?> AuthenticateAsync(ClaimsPrincipal user, IServiceProvider services, string? apiKey, CancellationToken cancellationToken)
    {
        if (!_accounts.Value.Enabled) return await _authentication.AuthenticatePrincipalAsync(apiKey, cancellationToken);

        // Resolved per request: the factory is only registered while accounts are on.
        var factory = services.GetService<SessionPrincipalFactory>();

        return factory == null ? null : await factory.CreateAsync(user);
    }
}
