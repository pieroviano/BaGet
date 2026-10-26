using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.ViewModels;

/// <summary>What the registration form posts.</summary>
public class RegisterForm
{
    /// <summary>The wanted user name.</summary>
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    /// <summary>The email address.</summary>
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>The password. Never echoed.</summary>
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>The password again. Never echoed.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    /// <summary>Where to go after signing in; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>The registration screen. A request binds <see cref="RegisterForm"/>, never this type; the passwords are always empty here.</summary>
public sealed class RegisterViewModel : RegisterForm
{
    /// <summary>Whether the account was created but waits for an administrator (<c>Accounts:RequireApproval</c>).</summary>
    public bool AwaitingApproval { get; set; }

    /// <summary>Whether a confirmation link was mailed.</summary>
    public bool ConfirmationSent { get; set; }

    /// <summary>Whether the screen asks for an email address.</summary>
    public bool EmailRequired { get; set; }
}
