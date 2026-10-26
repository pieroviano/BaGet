using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.ViewModels;

/// <summary>What the sign-in form posts (and the body of <c>POST /api/ui/account/login</c>).</summary>
public class LoginForm
{
    /// <summary>The user name.</summary>
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    /// <summary>The password. Never echoed: a screen's model leaves it empty.</summary>
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>Whether the session outlives the browser.</summary>
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// The sign-in screen: the form fields it repeats and what it shows. A request never binds this type (the form is <see cref="LoginForm"/>), so
/// the outputs cannot be set from outside; <see cref="LoginForm.Password"/> is always empty.
/// </summary>
public sealed class LoginViewModel : LoginForm
{
    /// <summary>Whether the registration link is shown.</summary>
    public bool AllowRegistration { get; set; }

    /// <summary>Whether sign-in with an external provider is offered.</summary>
    public bool HasExternalProvider { get; set; }

    /// <summary>The provider's display name.</summary>
    public string? ExternalProviderName { get; set; }

    /// <summary>Whether mail is configured (the forgot-password link is shown).</summary>
    public bool EmailIsConfigured { get; set; }

    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }

    /// <summary>The notice to show.</summary>
    public string? Notice { get; set; }
}
