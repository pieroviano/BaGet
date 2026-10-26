using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.ViewModels;

/// <summary>What the recovery-code form posts.</summary>
public class RecoveryCodeLoginForm
{
    /// <summary>The recovery code.</summary>
    [Required]
    [Display(Name = "Recovery code")]
    public string? RecoveryCode { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>The second step of signing in with a recovery code. A request binds <see cref="RecoveryCodeLoginForm"/>, never this type.</summary>
public sealed class RecoveryCodeLoginViewModel : RecoveryCodeLoginForm
{
    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }
}
