using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The registration screen.</summary>
public sealed class RegisterViewModel
{
    /// <summary>The wanted user name.</summary>
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    /// <summary>The email address.</summary>
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>The password.</summary>
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>The password again.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>Whether the account was created but waits for an administrator (<c>Accounts:RequireApproval</c>).</summary>
    [BindNever, ValidateNever]
    public bool AwaitingApproval { get; set; }

    /// <summary>Whether a confirmation link was mailed.</summary>
    [BindNever, ValidateNever]
    public bool ConfirmationSent { get; set; }

    /// <summary>Whether the screen asks for an email address.</summary>
    [BindNever, ValidateNever]
    public bool EmailRequired { get; set; }
}
