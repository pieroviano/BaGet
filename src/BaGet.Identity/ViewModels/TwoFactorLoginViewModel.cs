using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The second step of signing in: an authenticator code.</summary>
public sealed class TwoFactorLoginViewModel
{
    /// <summary>The code from the authenticator app.</summary>
    [Required]
    [Display(Name = "Authenticator code")]
    public string? Code { get; set; }

    /// <summary>Whether the session outlives the browser.</summary>
    public bool RememberMe { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>The refusal to show.</summary>
    [BindNever, ValidateNever]
    public string? Error { get; set; }
}
