using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account;

public class ForgotPasswordModel : AccountPageModel
{
    protected override bool RequiresEmail => true;

    [BindProperty]
    [Required]
    [Display(Name = "User name or email")]
    public string? Identifier { get; set; }

    public bool Sent { get; private set; }

    public void OnGet()
    {
    }

    /// <summary>
    /// Answers the same, in the same time, whether or not an account matches, whether or
    /// not the mail could be sent and whether or not the request was rate limited, so the
    /// page cannot be used to find out who has an account. The mail itself is sent off the
    /// request path.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var identifier = Identifier?.Trim();

        // Both lookups always run, so the work done does not depend on which one matches.
        #pragma warning disable CS8604
        var byName = await UserManager.FindByNameAsync(identifier);
        #pragma warning restore CS8604
        BaGetUser? byEmail = null;
        try
        {
            byEmail = await UserManager.FindByEmailAsync(identifier);
        }
        catch (InvalidOperationException)
        {
            // Several accounts share the address: none is singled out.
        }

        var user = byName ?? byEmail;

        // One link template either way; the user and token are filled in later.
        var template = PageLink("/Account/ResetPassword", PasswordResetLinkSender.Template);

        // Only an account that would be mailed uses up an allowance, so requests for names
        // that do not exist can neither fill the limiter nor crowd real accounts out of it.
        // The per-account limit is the protection; the client and site limits are backstops.
        var client = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        if (user != null
            && !string.IsNullOrEmpty(user.Email)
            && HttpContext.RequestServices.GetRequiredService<PasswordResetRateLimiter>().TryAcquire(client, user.Id))
        {
            PasswordResetLinkSender.TryEnqueue(HttpContext.RequestServices, user.Id, template);
        }

        Sent = true;

        return Page();
    }
}
