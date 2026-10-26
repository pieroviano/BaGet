using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.ViewModels;

/// <summary>What the "forgot password" form posts.</summary>
public class ForgotPasswordForm
{
    /// <summary>The user name or email address.</summary>
    [Required]
    [Display(Name = "User name or email")]
    public string? Identifier { get; set; }
}

/// <summary>The "forgot password" screen. A request binds <see cref="ForgotPasswordForm"/>, never this type.</summary>
public sealed class ForgotPasswordViewModel : ForgotPasswordForm
{
    /// <summary>Whether the request was answered (always the same answer, see the use case).</summary>
    public bool Sent { get; set; }
}
