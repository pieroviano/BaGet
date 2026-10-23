using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account;

public class ApiKeysModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    [BindProperty]
    [StringLength(AbstractContext<Microsoft.EntityFrameworkCore.DbContext>.MaxApiKeyDescriptionLength)]
    public string? Description { get; set; }

    [BindProperty]
    public List<string> Scopes { get; set; } = new List<string>();

    [BindProperty]
    [Display(Name = "Package glob pattern")]
    public string PackageGlobPattern { get; set; } = "*";

    [BindProperty]
    [Range(1, 3650)]
    [Display(Name = "Expires after (days)")]
    public int? ExpiresInDays { get; set; } = 365;

    /// <summary>
    /// The keys of the repositories the new key is limited to; none (the default) means every repository.
    /// </summary>
    [BindProperty]
    public List<int> RepositoryKeys { get; set; } = new List<int>();

    /// <summary>
    /// The repositories offered in the picker: those the user can read. Empty (picker hidden) where the server holds
    /// the default repository only.
    /// </summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; private set; } = Array.Empty<RepositoryInfo>();

    private IReadOnlyDictionary<int, IReadOnlyList<string>> _keyRepositories = new Dictionary<int, IReadOnlyList<string>>();

    public IReadOnlyList<ApiKey> Keys { get; private set; } = Array.Empty<ApiKey>();

    /// <summary>
    /// The key just created. Shown once: only its hash is kept.
    /// </summary>
    public string? CreatedKey { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (Scopes.Count == 0)
        {
            ModelState.AddModelError(nameof(Scopes), "Choose at least one scope.");
        }

        _administrator = await HttpContext.RequestServices.GetRequiredService<AdministratorService>().IsAdministratorAsync(User, cancellationToken);
        if (ModelState.IsValid)
        {
            // Only a repository the user can read may be chosen: a key limited to one nobody can see is a probe.
            await LoadRepositoriesAsync(cancellationToken);
            if (RepositoryKeys.Except(Repositories.Select(r => r.Key)).Any())
            {
                ModelState.AddModelError(nameof(RepositoryKeys), "Choose only repositories from the list.");
            }
        }

        if (ModelState.IsValid)
        {
            var now = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow;

            try
            {
                CreatedKey = await ApiKeys.CreateAsync(
                    UserManager.GetUserId(User),
                    Description,
                    Scopes,
                    PackageGlobPattern,
                    ExpiresInDays.HasValue ? now.AddDays(ExpiresInDays.Value) : (DateTime?)null,
                    RepositoryKeys,
                    cancellationToken);
            }
            catch (ArgumentException e)
            {
                ModelState.AddModelError(nameof(Scopes), e.Message);
            }
        }

        await LoadAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int key, CancellationToken cancellationToken)
    {
        await ApiKeys.RevokeAsync(UserManager.GetUserId(User), key, cancellationToken);

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _administrator = await HttpContext.RequestServices.GetRequiredService<AdministratorService>().IsAdministratorAsync(User, cancellationToken);
        Keys = await ApiKeys.ListAsync(UserManager.GetUserId(User), cancellationToken);
        _keyRepositories = await ApiKeys.RepositoryNamesAsync(Keys, cancellationToken);
        await LoadRepositoriesAsync(cancellationToken);
    }

    private async Task LoadRepositoriesAsync(CancellationToken cancellationToken)
    {
        var access = HttpContext.RequestServices.GetService<IRepositoryAccess>();
        Repositories = access == null || !access.SupportsMultipleRepositories
            ? Array.Empty<RepositoryInfo>()
            : await access.ReadableAsync(User, cancellationToken);
    }

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(ApiKey key)
        => ApiKeyService.DescribeRepositories(_keyRepositories.GetValueOrDefault(key.Key));

    public static string Describe(ApiKey key)
        => string.Join(", ", key.Scopes ?? Array.Empty<string>()) + " on " + key.PackageGlobPattern;

    /// <summary>
    /// The scopes the new-key form offers: the package scopes, plus <see cref="ApiKeyScopes.Admin"/> (the REST API under
    /// <c>/api/v1</c>) for an administrator.
    /// </summary>
    public IReadOnlyList<string> AllScopes => _administrator ? ApiKeyScopes.Assignable : ApiKeyScopes.All;

    private bool _administrator;
}