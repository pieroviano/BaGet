using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Lets a signed-in user write to the owners of a package without learning their addresses. The message goes to every owning user
/// and to the administrators of every owning organization, one email each, with the sender in Reply-To. Served by
/// <c>ContactOwnersController</c>.
/// </summary>
internal sealed class ContactOwnersWorkflow : PackageWorkflowBase
{
    private readonly ModelStateDictionary _modelState;

    /// <summary>Creates the workflow of one request over the (bound) form fields in <paramref name="model"/>.</summary>
    public ContactOwnersWorkflow(HttpContext httpContext, ModelStateDictionary modelState, ContactOwnersViewModel model, string? id)
        : base(httpContext)
    {
        _modelState = modelState ?? throw new ArgumentNullException(nameof(modelState));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Id = id;
    }

    /// <summary>The package ID from the address.</summary>
    public string? Id { get; set; }

    /// <summary>The form fields and what the screen shows.</summary>
    public ContactOwnersViewModel Model { get; }

    /// <summary>The GET handler: 404 when the package has no owner.</summary>
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        return await LoadAsync(cancellationToken) == null ? NotFound() : Show;
    }

    /// <summary>The POST handler: validates, finds the recipients, rate limits and sends.</summary>
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        var owners = await LoadAsync(cancellationToken);
        if (owners == null) return NotFound();

        var sender = await UserManager.GetUserAsync(User);
        if (sender == null) return Challenge();

        if (!Model.SenderHasEmail)
        {
            Model.Error = "Add an email address to your account and confirm it first, so the owners can reply.";
            return Show;
        }

        if (!_modelState.IsValid) return Show;

        var recipients = await FindRecipientsAsync(owners, cancellationToken);
        if (recipients.Count == 0)
        {
            Model.Error = "None of the owners can be reached by email.";
            return Show;
        }

        var limiter = HttpContext.RequestServices.GetRequiredService<ContactRateLimiter>();
        if (!limiter.TryAcquire(sender.Id, Model.PackageId))
        {
            Model.Error = "You can send one message per package every "
                          + (int)ContactRateLimiter.Window.TotalMinutes
                          + " minutes. Please try again later.";
            return Show;
        }

        var mail = HttpContext.RequestServices.GetRequiredService<IEmailMessageSender>();
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ContactOwnersWorkflow>>();
        var subject = $"Message about your package {Model.PackageId}";
        #pragma warning disable CS8602
        var body = BuildBody(sender, Model.Message.Trim(), copy: false);
        #pragma warning restore CS8602
        var delivered = 0;

        foreach (var recipient in recipients)
        {
            try
            {
                #pragma warning disable CS8602
                await mail.SendAsync(recipient, sender.Email.Trim(), sender.UserName, subject, body, cancellationToken);
                #pragma warning restore CS8602
                delivered++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not send a contact-owners message for {PackageId}.", Model.PackageId);
            }
        }

        if (delivered == 0)
        {
            limiter.Release(sender.Id, Model.PackageId);
            Model.Error = "The message could not be sent. Please try again later.";
            return Show;
        }

        if (Model.CopyToSelf)
        {
            try
            {
                #pragma warning disable CS8602
                await mail.SendAsync(
                    sender.Email.Trim(),
                    replyTo: null,
                    replyToName: null,
                    subject: $"Copy of your message about {Model.PackageId}",
                    textBody: BuildBody(sender, Model.Message.Trim(), copy: true),
                    cancellationToken: cancellationToken);
                #pragma warning restore CS8602
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not send the sender's copy of a contact-owners message for {PackageId}.", Model.PackageId);
            }
        }

        Model.Sent = true;
        return Show;
    }

    private async Task<IReadOnlyList<PackageOwner>?> LoadAsync(CancellationToken cancellationToken)
    {
        var owners = await Ownership.GetOwnersAsync(Id, cancellationToken);
        if (owners.Count == 0) return null;

        Model.PackageId = owners[0].PackageId;

        var user = await UserManager.GetUserAsync(User);
        Model.SenderHasEmail = user != null && user.EmailConfirmed && IsDeliverable(user.Email);

        return owners;
    }

    /// <summary>The confirmed addresses of the user owners and of the administrators of the owning organizations, each once.</summary>
    private async Task<IReadOnlyList<string>> FindRecipientsAsync(
        IReadOnlyList<PackageOwner> owners,
        CancellationToken cancellationToken)
    {
        var context = HttpContext.RequestServices.GetRequiredService<IContext>();
        #pragma warning disable CS8602
        var names = owners.Select(o => o.OwnerName.ToLowerInvariant()).ToList();
        #pragma warning restore CS8602

        #pragma warning disable CS8602
        var userIds = await UserManager.Users
            .AsNoTracking()
            .Where(u => names.Contains(u.UserName.ToLower()))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602

        #pragma warning disable CS8602, CS8604
        var adminIds = await context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.IsAdmin && m.Organization != null && names.Contains(m.Organization.Name.ToLower()))
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        var ids = userIds.Concat(adminIds).Distinct().ToList();
        var emails = await UserManager.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.EmailConfirmed)
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        #pragma warning disable CS8602
        return emails
            .Where(IsDeliverable)
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        #pragma warning restore CS8602
    }

    private static bool IsDeliverable(string? email)
        => !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email.Trim());

    private string BuildBody(BaGetUser sender, string message, bool copy)
    {
        var text = new StringBuilder();
        if (copy)
        {
            text.Append("This is a copy of the message you sent to the owners of ").Append(Model.PackageId).Append(".\n\n");
        }
        else
        {
            text.Append(sender.UserName).Append(" sent you this message about the package ").Append(Model.PackageId)
                .Append(". Reply to this email to answer them.\n\n");
        }

        text.Append("----\n").Append(message.Replace("\r\n", "\n").Replace('\r', '\n')).Append("\n----\n");
        if (!copy)
        {
            text.Append("\nYou received this because you own ").Append(Model.PackageId).Append(". Your address was not shown to the sender.\n");
        }

        return text.ToString();
    }
}
