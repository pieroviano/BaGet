using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.Application.Models;

/// <summary>What the authenticator-code form posts.</summary>
public class TwoFactorLoginForm
{
    /// <summary>The code from the authenticator app.</summary>
    [Required]
    [Display(Name = "Authenticator code")]
    public string? Code { get; set; }

    /// <summary>Whether the session outlives the browser.</summary>
    public bool RememberMe { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>The second step of signing in: an authenticator code. A request binds <see cref="TwoFactorLoginForm"/>, never this type.</summary>
public sealed class TwoFactorLoginViewModel : TwoFactorLoginForm
{
    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }
}
