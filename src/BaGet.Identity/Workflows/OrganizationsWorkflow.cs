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
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The organization screens of the administrator: the list, and one organization: rename it (package ownership follows in every
/// repository, atomically), add, promote and remove members (an organization keeps at least one administrator) and delete it
/// (refused while it owns packages). Every action asks for the administrator's password again and is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed. Shared by <c>AdminOrganizationsController</c> and the obsolete
/// <c>OrganizationsModel</c> and <c>OrganizationDetailModel</c>.
/// </summary>
internal sealed class OrganizationsWorkflow : AdminWorkflow
{
    public OrganizationsWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The list; filled by <see cref="ListAsync"/>.</summary>
    public OrganizationsViewModel List { get; } = new();

    /// <summary>One organization; the caller sets <c>Name</c> first.</summary>
    public OrganizationDetailViewModel Model { get; } = new();

    /// <summary>Lists the organizations.</summary>
    public async Task ListAsync(CancellationToken cancellationToken)
        => List.Organizations = await Ownership.GetAllOrganizationsAsync(cancellationToken);

    /// <summary>Shows the organization; 404 when there is none.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken) ? null : new NotFoundResult();

    /// <summary>Renames the organization.</summary>
    public Task<IActionResult?> RenameAsync(string? newName, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var result = await Ownership.RenameOrganizationAsync(Model.Name, newName, cancellationToken);
            if (result == OrganizationChange.Success)
            {
                Log(current, "renamed organization {Old} to {New}", Model.Name, newName?.Trim());
                Model.Name = newName!.Trim();
                Model.Message = $"The organization is now called {Model.Name}.";
            }
            else
            {
                Model.Error = Describe(result);
            }
        });

    /// <summary>Deletes the organization.</summary>
    public async Task<IActionResult?> DeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var result = await ActAsync(password, async current =>
        {
            var change = await Ownership.DeleteOrganizationAsync(Model.Name, cancellationToken, current.UserName);
            if (change == OrganizationChange.Success)
            {
                Log(current, "deleted organization {Old}", Model.Name, null);
                Model.Message = $"{Model.Name} was deleted."; // flashed to the list
                deleted = true;
            }
            else
            {
                Model.Error = Describe(change);
            }
        });

        return deleted ? RedirectToScreen("/Admin/Organizations") : result;
    }

    /// <summary>Adds a user to the organization.</summary>
    public Task<IActionResult?> AddMemberAsync(string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
            if (user == null)
            {
                Model.Error = "There is no user with that name.";
            }
            else if (await Ownership.AddMemberAsync(Model.Name!, user.Id, isAdmin, cancellationToken))
            {
                Log(current, "added {New} to organization {Old}", Model.Name, user.UserName);
                Model.Message = $"{user.UserName} joined {Model.Name}.";
            }
            else
            {
                Model.Error = $"{user.UserName} is already a member.";
            }
        });

    /// <summary>Makes a member an administrator of the organization, or not.</summary>
    public Task<IActionResult?> SetAdminAsync(string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
            var result = user == null
                ? OrganizationChange.NotFound
                : await Ownership.SetMemberAdminAsync(Model.Name!, user.Id, isAdmin, cancellationToken);
            if (result == OrganizationChange.Success)
            {
                Log(current, "set administrator={New} for a member of organization {Old}", Model.Name, isAdmin.ToString());
                Model.Message = isAdmin ? $"{user!.UserName} now administers {Model.Name}." : $"{user!.UserName} no longer administers {Model.Name}.";
            }
            else
            {
                Model.Error = Describe(result);
            }
        });

    /// <summary>Removes a member from the organization.</summary>
    public Task<IActionResult?> RemoveMemberAsync(string? userName, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
            if (user == null)
            {
                Model.Error = "There is no user with that name.";
            }
            else if (await Ownership.RemoveMemberAsync(Model.Name!, user.Id, cancellationToken))
            {
                Log(current, "removed {New} from organization {Old}", Model.Name, user.UserName);
                Model.Message = $"{user.UserName} left {Model.Name}.";
            }
            else
            {
                Model.Error = "An organization must keep at least one administrator, and the user must be a member.";
            }
        });

    private static string Describe(OrganizationChange change) => change switch
    {
        OrganizationChange.NameTaken => "That name is taken.",
        OrganizationChange.InvalidName => "That is not a valid name: it must not be blank or contain / \\ ? # %.",
        OrganizationChange.OwnsPackages => "The organization owns packages, so it cannot be deleted. Remove it as an owner of those packages first.",
        OrganizationChange.LastAdministrator => "An organization must keep at least one administrator.",
        _ => "There is no such organization or member.",
    };

    private void Log(BaGetUser admin, string template, string? oldValue, string? newValue)
        => Services.GetRequiredService<ILogger<OrganizationsWorkflow>>()
            .LogWarning("{Admin} " + template, admin.UserName, oldValue, newValue);

    // Refuse during maintenance, confirm the password, run the change, then show the (possibly renamed) screen.
    private async Task<IActionResult?> ActAsync(string? password, Func<BaGetUser, Task> change)
    {
        ThrowIfClosed();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (await Ownership.FindOrganizationAsync(Model.Name, Http.RequestAborted) == null) return new NotFoundResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            await change(current);
        }

        return RedirectToScreen("/Admin/OrganizationDetail", new { name = Model.Name });
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Model.Organization = await Ownership.FindOrganizationAsync(Model.Name, cancellationToken);
        if (Model.Organization == null) return false;

        var rows = new List<OrganizationMemberRow>();
        foreach (var member in await Ownership.GetMembersAsync(Model.Organization.Name, cancellationToken))
        {
            #pragma warning disable CS8604
            var user = await UserManager.FindByIdAsync(member.UserId);
            #pragma warning restore CS8604
            if (user != null) rows.Add(new OrganizationMemberRow(user.UserName, member.IsAdmin));
        }

        Model.Members = rows.OrderBy(r => r.UserName, StringComparer.OrdinalIgnoreCase).ToList();
        Model.OwnedPackageIds = await Ownership.CountOwnedPackageIdsAsync(Model.Organization.Name, cancellationToken);

        await LoadConfirmationAsync(Model, "/Admin/OrganizationDetail");

        return true;
    }
}
