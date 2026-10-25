using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The screen behind the link of a password reset email.</summary>
public sealed class ResetPasswordViewModel
{
    /// <summary>The account named by the link.</summary>
    public string? UserId { get; set; }

    /// <summary>The encoded reset token of the link.</summary>
    public string? Code { get; set; }

    /// <summary>The new password.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? Password { get; set; }

    /// <summary>The new password again.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    /// <summary>The refusal to show.</summary>
    [BindNever, ValidateNever]
    public string? Error { get; set; }

    /// <summary>Whether the link carries a user and a token.</summary>
    [BindNever, ValidateNever]
    public bool LinkIsUsable => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(Code);
}
