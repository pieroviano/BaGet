namespace BaGet.Identity.ViewModels;

/// <summary>What the "sign in again" button posts.</summary>
public class ReauthenticateForm
{
    /// <summary>Where to go after the provider answered; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>The "confirm it is you" screen of an account without a password. A request binds <see cref="ReauthenticateForm"/>, never this type.</summary>
public sealed class ReauthenticateViewModel : ReauthenticateForm
{
    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }
}
