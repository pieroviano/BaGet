using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Account;

public class ForgotPasswordModel : AccountPageModel
{
    protected override bool RequiresEmail => true;

    [BindProperty]
    [Required]
    [Display(Name = "User name or email")]
    public string Identifier { get; set; }

    public bool Sent { get; private set; }

    public void OnGet()
    {
    }

    /// <summary>
    /// Answers the same whether or not an account matches, and whether or not the mail
    /// could be sent, so the page cannot be used to find out who has an account.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await UserManager.FindByNameAsync(Identifier.Trim());
        if (user == null)
        {
            try
            {
                user = await UserManager.FindByEmailAsync(Identifier.Trim());
            }
            catch (InvalidOperationException)
            {
                // Several accounts share the address: none is singled out.
            }
        }

        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            var token = await UserManager.GeneratePasswordResetTokenAsync(user);
            var link = PageLink("/Account/ResetPassword", new { userId = user.Id, code = EncodeToken(token) });

            try
            {
                await EmailSender.SendPasswordResetLinkAsync(user, user.Email, link);
            }
            catch (Exception e)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<ForgotPasswordModel>>()
                    .LogError(e, "Could not send a password reset link.");
            }
        }

        Sent = true;

        return Page();
    }
}
