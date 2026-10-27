namespace BaGet.Identity.Application.Models.Account;

/// <summary>The "confirm it is you" screen of an account without a password. A request binds <see cref="ReauthenticateForm"/>, never this type.</summary>
public sealed class ReauthenticateViewModel : ReauthenticateForm
{
    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }
}
