using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
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

    private const string UserIdPlaceholder = "USERIDPLACEHOLDER";
    private const string CodePlaceholder = "CODEPLACEHOLDER";

    /// <summary>
    /// Answers the same, in the same time, whether or not an account matches, whether or
    /// not the mail could be sent and whether or not the request was rate limited, so the
    /// page cannot be used to find out who has an account. The mail itself is sent off the
    /// request path.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var identifier = Identifier.Trim();

        // Both lookups always run, so the work done does not depend on which one matches.
        var byName = await UserManager.FindByNameAsync(identifier);
        BaGetUser byEmail = null;
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
        var template = PageLink("/Account/ResetPassword", new { userId = UserIdPlaceholder, code = CodePlaceholder });

        var limiter = HttpContext.RequestServices.GetRequiredService<PasswordResetRateLimiter>();
        var client = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var allowed = limiter.TryAcquire(client, user?.Id ?? "name:" + identifier.ToLowerInvariant());

        if (allowed && user != null && !string.IsNullOrEmpty(user.Email))
        {
            var userId = user.Id;
            HttpContext.RequestServices.GetRequiredService<BackgroundEmailQueue>()
                .TryEnqueue(services => SendAsync(services, userId, template));
        }

        Sent = true;

        return Page();
    }

    private static async Task SendAsync(IServiceProvider services, string userId, string template)
    {
        var users = services.GetRequiredService<UserManager<BaGetUser>>();
        var user = await users.FindByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.Email)) return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = template
            .Replace(UserIdPlaceholder, Uri.EscapeDataString(user.Id))
            .Replace(CodePlaceholder, EncodeToken(token));

        try
        {
            await services.GetRequiredService<IEmailSender<BaGetUser>>().SendPasswordResetLinkAsync(user, user.Email, link);
        }
        catch (Exception e)
        {
            services.GetRequiredService<ILogger<ForgotPasswordModel>>().LogError(e, "Could not send a password reset link.");
        }
    }
}
