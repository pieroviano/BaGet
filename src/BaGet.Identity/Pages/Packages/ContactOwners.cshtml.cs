using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// Lets a signed-in user write to the owners of a package without learning their addresses.
/// The message goes to every owning user and to the administrators of every owning
/// organization, one email each, with the sender in Reply-To.
/// </summary>
public class ContactOwnersModel : AccountPageModel
{
    public const int MaxMessageLength = 4000;

    protected override bool RequiresSignIn => true;

    protected override bool RequiresEmail => true;

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; }

    [BindProperty]
    [Required]
    [StringLength(MaxMessageLength)]
    [Display(Name = "Message")]
    public string Message { get; set; }

    [BindProperty]
    [Display(Name = "Send me a copy")]
    public bool CopyToSelf { get; set; }

    /// <summary>
    /// The package ID as stored, which is the only text that reaches the subject.
    /// </summary>
    public string PackageId { get; private set; }

    public bool Sent { get; private set; }

    public string Error { get; private set; }

    /// <summary>
    /// Replies come to the sender's address, so there must be one.
    /// </summary>
    public bool SenderHasEmail { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await LoadAsync(cancellationToken) == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var owners = await LoadAsync(cancellationToken);
        if (owners == null) return NotFound();

        var sender = await UserManager.GetUserAsync(User);
        if (sender == null) return Challenge();

        if (!SenderHasEmail)
        {
            Error = "Add an email address to your account first, so the owners can reply.";
            return Page();
        }

        if (!ModelState.IsValid) return Page();

        var recipients = await FindRecipientsAsync(owners, cancellationToken);
        if (recipients.Count == 0)
        {
            Error = "None of the owners can be reached by email.";
            return Page();
        }

        var limiter = HttpContext.RequestServices.GetRequiredService<ContactRateLimiter>();
        if (!limiter.TryAcquire(sender.Id, PackageId))
        {
            Error = "You can send one message per package every "
                    + (int)ContactRateLimiter.Window.TotalMinutes
                    + " minutes. Please try again later.";
            return Page();
        }

        var mail = HttpContext.RequestServices.GetRequiredService<IEmailMessageSender>();
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ContactOwnersModel>>();
        var subject = $"Message about your package {PackageId}";
        var body = BuildBody(sender, Message.Trim(), copy: false);
        var delivered = 0;

        foreach (var recipient in recipients)
        {
            try
            {
                await mail.SendAsync(recipient, sender.Email.Trim(), sender.UserName, subject, body, cancellationToken);
                delivered++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not send a contact-owners message for {PackageId}.", PackageId);
            }
        }

        if (delivered == 0)
        {
            limiter.Release(sender.Id, PackageId);
            Error = "The message could not be sent. Please try again later.";
            return Page();
        }

        if (CopyToSelf)
        {
            try
            {
                await mail.SendAsync(
                    sender.Email.Trim(),
                    replyTo: null,
                    replyToName: null,
                    subject: $"Copy of your message about {PackageId}",
                    textBody: BuildBody(sender, Message.Trim(), copy: true),
                    cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not send the sender's copy of a contact-owners message for {PackageId}.", PackageId);
            }
        }

        Sent = true;
        return Page();
    }

    private async Task<IReadOnlyList<PackageOwner>> LoadAsync(CancellationToken cancellationToken)
    {
        var owners = await Ownership.GetOwnersAsync(Id, cancellationToken);
        if (owners.Count == 0) return null;

        PackageId = owners[0].PackageId;

        var user = await UserManager.GetUserAsync(User);
        SenderHasEmail = user != null && IsDeliverable(user.Email);

        return owners;
    }

    /// <summary>
    /// The confirmed addresses of the user owners and of the administrators of the owning
    /// organizations, each once.
    /// </summary>
    private async Task<IReadOnlyList<string>> FindRecipientsAsync(
        IReadOnlyList<PackageOwner> owners,
        CancellationToken cancellationToken)
    {
        var context = HttpContext.RequestServices.GetRequiredService<IContext>();
        var names = owners.Select(o => o.OwnerName.ToLowerInvariant()).ToList();

        var userIds = await UserManager.Users
            .AsNoTracking()
            .Where(u => names.Contains(u.UserName.ToLower()))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var adminIds = await context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.IsAdmin && m.Organization != null && names.Contains(m.Organization.Name.ToLower()))
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var ids = userIds.Concat(adminIds).Distinct().ToList();
        var emails = await UserManager.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.EmailConfirmed)
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        return emails
            .Where(IsDeliverable)
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsDeliverable(string email)
        => !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email.Trim());

    private string BuildBody(BaGetUser sender, string message, bool copy)
    {
        var text = new StringBuilder();
        if (copy)
        {
            text.Append("This is a copy of the message you sent to the owners of ").Append(PackageId).Append(".\n\n");
        }
        else
        {
            text.Append(sender.UserName).Append(" sent you this message about the package ").Append(PackageId)
                .Append(". Reply to this email to answer them.\n\n");
        }

        text.Append("----\n").Append(message.Replace("\r\n", "\n").Replace('\r', '\n')).Append("\n----\n");
        if (!copy)
        {
            text.Append("\nYou received this because you own ").Append(PackageId).Append(". Your address was not shown to the sender.\n");
        }

        return text.ToString();
    }
}
