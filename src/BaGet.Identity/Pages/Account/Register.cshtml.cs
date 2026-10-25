using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Account;

public class RegisterModel : AccountPageModel
{
    [BindProperty]
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    [BindProperty]
    [EmailAddress]
    public string? Email { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        base.OnPageHandlerExecuting(context);

        if (context.Result == null && !Accounts.AllowRegistration)
        {
            context.Result = NotFound();
        }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (EmailConfigured && string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(nameof(Email), "An email address is required to confirm the account.");
        }

        if (!ModelState.IsValid) return Page();

        // User and organization names share a namespace: either can own a package.
        if (await Ownership.AccountExistsAsync(UserName, cancellationToken))
        {
            ModelState.AddModelError(nameof(UserName), "That name is taken.");
            return Page();
        }

        var user = new BaGetUser
        {
            UserName = UserName,
            Email = Email,
            Created = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow,
            PendingApproval = Accounts.RequireApproval,
        };

        #pragma warning disable CS8604
        var result = await UserManager.CreateAsync(user, Password);
        #pragma warning restore CS8604
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        if (user.PendingApproval)
        {
            HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RegisterModel>>()
                .LogWarning("{User} registered and awaits approval", user.UserName);
        }

        if (EmailConfigured)
        {
            // Signing in needs a confirmed address, so the user is not signed in yet.
            var token = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            var link = PageLink("/Account/ConfirmEmail", new { userId = user.Id, code = EncodeToken(token) });

            try
            {
                #pragma warning disable CS8604
                await EmailSender.SendConfirmationLinkAsync(user, user.Email, link);
                #pragma warning restore CS8604
                ConfirmationSent = true;
            }
            catch (Exception)
            {
                // The account exists; "Forgot password" mails a link that also confirms the address.
                ModelState.AddModelError(string.Empty, "Your account was created, but we could not send the confirmation email. Use \"Forgot password\" to get a link later.");
            }

            return Page();
        }

        if (user.PendingApproval)
        {
            // Not signed in: an administrator enables the account first.
            AwaitingApproval = true;

            return Page();
        }

        await SignInManager.SignInAsync(user, isPersistent: false);

        return RedirectToLocal(ReturnUrl);
    }

    /// <summary>Whether the account was created but waits for an administrator (<c>Accounts:RequireApproval</c>).</summary>
    public bool AwaitingApproval { get; private set; }

    public bool ConfirmationSent { get; private set; }

    public bool EmailRequired => EmailConfigured;
}