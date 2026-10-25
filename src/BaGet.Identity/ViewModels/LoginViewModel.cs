using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The sign-in screen: the form fields and what the screen shows. Outputs are never bound from the request.</summary>
public sealed class LoginViewModel
{
    /// <summary>The user name.</summary>
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    /// <summary>The password.</summary>
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>Whether the session outlives the browser.</summary>
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>Whether the registration link is shown.</summary>
    [BindNever, ValidateNever]
    public bool AllowRegistration { get; set; }

    /// <summary>Whether sign-in with an external provider is offered.</summary>
    [BindNever, ValidateNever]
    public bool HasExternalProvider { get; set; }

    /// <summary>The provider's display name.</summary>
    [BindNever, ValidateNever]
    public string? ExternalProviderName { get; set; }

    /// <summary>Whether mail is configured (the forgot-password link is shown).</summary>
    [BindNever, ValidateNever]
    public bool EmailIsConfigured { get; set; }

    /// <summary>The refusal to show.</summary>
    [BindNever, ValidateNever]
    public string? Error { get; set; }

    /// <summary>The notice to show.</summary>
    [BindNever, ValidateNever]
    public string? Notice { get; set; }
}
