using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Lists the repositories with their feed addresses and creates new ones: local, remote (an upstream cache, see
/// <see cref="RemoteRepositorySettings"/>) or virtual (an ordered group of members, filled in on its edit screen), of any format
/// whose module offers the kind; the name is validated by <see cref="RepositoryNames"/>. Creating is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed. Creating a remote repository, which can carry credentials, asks for the administrator's
/// password again. Shared by <c>AdminRepositoriesController</c> and the obsolete <c>RepositoriesModel</c>.
/// </summary>
internal sealed class RepositoriesWorkflow : AdminWorkflow
{
    public RepositoriesWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
        Model.Http = httpContext;
        Model.UrlHelper = url;
    }

    /// <summary>The state the screen renders; the caller sets <c>Name</c> and <c>Remote</c> from the posted fields first.</summary>
    public RepositoriesViewModel Model { get; } = new();

    private FormatRegistry Formats => Services.GetRequiredService<FormatRegistry>();

    /// <summary>
    /// Lists the repositories, narrowed by <c>format</c>, <c>kind</c> and <c>q</c> (a name fragment) like
    /// <c>GET /api/v1/repositories</c>, through <see cref="RepositorySummaryFilter"/>.
    /// </summary>
    public async Task GetAsync(CancellationToken cancellationToken)
    {
        var query = Http.Request.Query;
        string? Query(string key) => string.IsNullOrWhiteSpace(query[key]) ? null : query[key].ToString().Trim();
        Model.FilterFormat = Query("format");
        Model.FilterKind = Query("kind");
        Model.FilterName = Query("q");

        await LoadAsync(cancellationToken);
        Model.Repositories = RepositorySummaryFilter.Apply(Model.Repositories, Model.FilterFormat, Model.FilterKind, Model.FilterName).ToList();
    }

    /// <summary>Creates a local NuGet repository.</summary>
    public async Task<IActionResult?> PostAsync(CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var name = Model.Name?.Trim();
        if (!RepositoryNames.IsValid(name, out var invalid))
        {
            return await RefuseAsync(invalid, cancellationToken);
        }

        try
        {
            await Services.GetRequiredService<IRepositoryService>()
                .CreateAsync(name!, RepositoryFormat.NuGet, RepositoryKind.Local, cancellationToken);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        Model.Message = $"Repository {name} was created.";

        return RedirectToScreen("/Admin/Repositories");
    }

    /// <summary>
    /// Creates a local repository of the format named by <paramref name="format"/> from the posted fields (<c>field:{name}</c>,
    /// the format module's <see cref="IFormatModule.CreateFields"/>). Refused with 503 while the gate is closed; 404 for a
    /// format with no module.
    /// </summary>
    public async Task<IActionResult?> PostFormatAsync(string? format, bool anonymousRead, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var module = Formats.Modules.FirstOrDefault(m => string.Equals(m.Format.ToString(), format, StringComparison.OrdinalIgnoreCase) && m.Format != RepositoryFormat.NuGet);
        if (module == null || !module.CreatableKinds.Contains(RepositoryKind.Local)) return new NotFoundResult();

        Model.Format = module.Format.ToString();
        var name = Model.Name?.Trim();
        if (!RepositoryNames.IsValid(name, out var invalid))
        {
            return await RefuseAsync(invalid, cancellationToken);
        }

        var form = Http.Request.Form;
        var fields = form.Keys
            .Where(k => k.StartsWith("field:", StringComparison.Ordinal))
            .ToDictionary(k => k.Substring("field:".Length), k => (string?)form[k].ToString(), StringComparer.Ordinal);

        try
        {
            await Services.GetRequiredService<IRepositoryService>()
                .CreateAsync(name!, module.Format, RepositoryKind.Local, fields, anonymousRead, cancellationToken);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException or ArgumentException)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        Model.Message = $"{module.DisplayName} repository {name} was created.";

        return RedirectToScreen("/Admin/Repositories");
    }

    /// <summary>Creates a virtual repository (NuGet unless <paramref name="format"/> says otherwise) with no members; they are added on its screen.</summary>
    public async Task<IActionResult?> PostVirtualAsync(string? format, bool anonymousRead, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var module = ModuleOf(format, m => m.SupportsVirtual);
        if (module == null) return new NotFoundResult();

        var name = Model.Name?.Trim();
        if (!RepositoryNames.IsValid(name, out var invalid))
        {
            return await RefuseAsync(invalid, cancellationToken);
        }

        try
        {
            await Services.GetRequiredService<IVirtualRepositoryAdministration>()
                .CreateAsync(name!, new VirtualRepositorySettings(), cancellationToken, User.Identity?.Name, anonymousRead, module.Format);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException or ArgumentException)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        Model.Message = $"Virtual {Describe(module)}repository {name} was created. Add its members below.";

        return RedirectToScreen("/Admin/RepositoryEdit", new { name });
    }

    /// <summary>Creates a remote repository (NuGet unless <paramref name="format"/> says otherwise) from the posted upstream settings.</summary>
    public async Task<IActionResult?> PostRemoteAsync(string? password, string? anonymousRead, string? format, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var module = ModuleOf(format, m => m.CreatableKinds.Contains(RepositoryKind.Remote));
        if (module == null) return new NotFoundResult();
        Model.Format = module.Format.ToString();

        var current = await UserManager.GetUserAsync(User);
        if (current == null || !await IsConfirmedAsync(current, password))
        {
            return await RefuseAsync("The confirmation is incorrect, so nothing was changed.", cancellationToken);
        }

        var name = Model.Name?.Trim();
        if (!RepositoryNames.IsValid(name, out var invalid))
        {
            return await RefuseAsync(invalid, cancellationToken);
        }

        try
        {
            // Absent = the default: readable by anonymous clients only when no credentials are configured.
            var settings = Model.Remote.ToSettings();
            var anonymous = string.IsNullOrEmpty(anonymousRead) ? !settings.HasCredentials : string.Equals(anonymousRead, "true", StringComparison.OrdinalIgnoreCase);
            await Services.GetRequiredService<IRepositoryService>()
                .CreateAsync(name!, module.Format, RepositoryKind.Remote, settings, anonymous, cancellationToken);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException or ArgumentException or InvalidOperationException)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        Model.Message = $"Remote {Describe(module)}repository {name} was created.";

        return RedirectToScreen("/Admin/RepositoryEdit", new { name });
    }

    // "npm " for a format other than NuGet, whose wording predates the format layer ("Remote repository up was created.").
    private static string Describe(IFormatModule module) => module.Format == RepositoryFormat.NuGet ? string.Empty : module.DisplayName + " ";

    // The module named by a posted format (NuGet when none), when it passes 'offers'; null otherwise.
    private IFormatModule? ModuleOf(string? format, Func<IFormatModule, bool> offers)
    {
        var module = string.IsNullOrEmpty(format)
            ? Formats.Find(RepositoryFormat.NuGet)
            : Formats.Modules.FirstOrDefault(m => string.Equals(m.Format.ToString(), format, StringComparison.OrdinalIgnoreCase));

        return module != null && offers(module) ? module : null;
    }

    // Shows the screen again with a reason, keeping what was typed.
    private async Task<IActionResult?> RefuseAsync(string? error, CancellationToken cancellationToken)
    {
        Model.Error = error;
        await LoadAsync(cancellationToken);

        return null;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadConfirmationAsync(Model, Http.Request.Path + Http.Request.QueryString);
        Model.CanCreate = Services.GetRequiredService<IRepositoryService>().SupportsMultipleRepositories;
        Model.AllFormats = Formats.Modules.ToList();
        Model.RemoteFormats = Formats.Modules.Where(m => m.CreatableKinds.Contains(RepositoryKind.Remote)).ToList();
        Model.VirtualFormats = Formats.Modules.Where(m => m.SupportsVirtual).ToList();
        Model.OtherFormats = Formats.Modules.Where(m => m.Format != RepositoryFormat.NuGet && m.CreatableKinds.Contains(RepositoryKind.Local)).ToList();

        Model.Repositories = await Services.GetRequiredService<IRepositoryAdministration>().ListAsync(cancellationToken);

        var virtuals = Services.GetRequiredService<IVirtualRepositoryAdministration>();
        var deployable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var repository in Model.Repositories.Where(r => r.Kind == RepositoryKind.Virtual))
        {
            if ((await virtuals.FindAsync(repository.Name, cancellationToken))?.DeployTarget != null) deployable.Add(repository.Name);
        }

        Model.SetDeployable(deployable);
    }
}
