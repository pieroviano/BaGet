using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account;

public class RegisterModel : AccountPageModel
{
    [BindProperty]
    [Required]
    [Display(Name = "User name")]
    public string UserName { get; set; }

    [BindProperty]
    [EmailAddress]
    public string Email { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

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
        };

        var result = await UserManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await SignInManager.SignInAsync(user, isPersistent: false);

        return RedirectToLocal(ReturnUrl);
    }
}