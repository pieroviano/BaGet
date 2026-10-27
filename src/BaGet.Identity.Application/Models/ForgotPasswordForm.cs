using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.Application.Models;

/// <summary>What the "forgot password" form posts.</summary>
public class ForgotPasswordForm
{
    /// <summary>The user name or email address.</summary>
    [Required]
    [Display(Name = "User name or email")]
    public string? Identifier { get; set; }
}
