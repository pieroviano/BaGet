using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The "forgot password" screen.</summary>
public sealed class ForgotPasswordViewModel
{
    /// <summary>The user name or email address.</summary>
    [Required]
    [Display(Name = "User name or email")]
    public string? Identifier { get; set; }

    /// <summary>Whether the request was answered (always the same answer, see the workflow).</summary>
    [BindNever, ValidateNever]
    public bool Sent { get; set; }
}
