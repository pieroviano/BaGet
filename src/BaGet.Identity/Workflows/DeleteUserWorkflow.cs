using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Workflows;

/// <summary>
/// An administrator deleting another account: what goes, what happens to the organizations and to the package IDs only that user
/// owned (leave them without owners, or delete them with all their versions), then a password confirmation. Never the
/// administrator's own account, and never the last administrator. Used by <c>AdminUsersController</c>.
/// </summary>
internal sealed class DeleteUserWorkflow : AdminWorkflow
{
    public DeleteUserWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The state the screen renders; the caller sets <c>Name</c> and <c>SoleOwned</c> first.</summary>
    public DeleteUserViewModel Model { get; } = new();

    /// <summary>The message to flash to the next request (the Users screen), or null.</summary>
    public string? RedirectMessage { get; private set; }

    /// <summary>The error to flash to the next request (the Users screen), or null.</summary>
    public string? RedirectError { get; private set; }

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
    {
        var (user, refusal) = await FindTargetAsync();
        if (refusal != null) return refusal;

        await LoadAsync(user!, cancellationToken);

        return null;
    }

    /// <summary>Deletes the user after the administrator's password, then returns to the users.</summary>
    public async Task<IActionResult?> PostAsync(string? password, CancellationToken cancellationToken)
    {
        var (user, refusal) = await FindTargetAsync();
        if (refusal != null) return refusal;

        var current = await UserManager.GetUserAsync(User);
        var deletePackages = string.Equals(Model.SoleOwned, "delete", StringComparison.OrdinalIgnoreCase);

        // The same refusal as the package page's hard delete: read-only mode freezes package removal.
        if (deletePackages && IsReadOnlyMode)
        {
            Model.Error = DeleteUserViewModel.ReadOnlyMessage;
            await LoadAsync(user!, cancellationToken);

            return null;
        }

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = "The confirmation is incorrect, so the account was not deleted.";
            await LoadAsync(user!, cancellationToken);

            return null;
        }

        var name = user!.UserName;
        var outcome = await Services.GetRequiredService<AccountDeletionService>()
            .DeleteByAdministratorAsync(user, deletePackages, current?.UserName, cancellationToken);

        if (!outcome.Result.Succeeded)
        {
            Model.Error = Describe(outcome.Result);
            await LoadAsync(user, cancellationToken);

            return null;
        }

        RedirectMessage = Summarize(name, outcome);
        if (outcome.Failures.Count > 0)
        {
            RedirectError = "Could not delete: " + string.Join(", ", outcome.Failures) + ". They have no owners and are listed under Packages without owners.";
        }

        return RedirectToScreen("/Admin/Users");
    }

    private bool IsReadOnlyMode => Services.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.IsReadOnlyMode;

    private static string Summarize(string? name, AdminDeletionResult outcome)
    {
        var text = $"{name} was deleted.";
        if (outcome.DeletedPackageIds.Count > 0) text += $" Packages deleted: {string.Join(", ", outcome.DeletedPackageIds)}.";
        if (outcome.KeptWithoutOwners.Count > 0) text += $" Packages left without owners: {string.Join(", ", outcome.KeptWithoutOwners)}.";

        return text;
    }

    // The user, or what to answer instead: 404 for no such user, back to the list for oneself.
    private async Task<(BaGetUser? User, IActionResult? Refusal)> FindTargetAsync()
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return (null, new ChallengeResult());

        var user = string.IsNullOrEmpty(Model.Name) ? null : await UserManager.FindByNameAsync(Model.Name);
        if (user == null) return (null, new NotFoundResult());

        if (user.Id == current.Id)
        {
            RedirectError = "Delete your own account from your account settings.";

            return (null, RedirectToScreen("/Admin/Users"));
        }

        return (user, null);
    }

    private async Task LoadAsync(BaGetUser user, CancellationToken cancellationToken)
    {
        var deletion = Services.GetRequiredService<AccountDeletionService>();
        Model.Preview = await deletion.PreviewAdministratorDeletionAsync(user, cancellationToken);
        Model.LastAdministrator = (await deletion.GetBlockersAsync(user, cancellationToken)).LastAdministrator;
        Model.IsReadOnlyMode = IsReadOnlyMode;

        await LoadConfirmationAsync(Model);
    }
}
