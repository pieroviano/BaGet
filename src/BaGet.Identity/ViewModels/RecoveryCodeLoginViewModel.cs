using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The second step of signing in with a recovery code.</summary>
public sealed class RecoveryCodeLoginViewModel
{
    /// <summary>The recovery code.</summary>
    [Required]
    [Display(Name = "Recovery code")]
    public string? RecoveryCode { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>The refusal to show.</summary>
    [BindNever, ValidateNever]
    public string? Error { get; set; }
}
