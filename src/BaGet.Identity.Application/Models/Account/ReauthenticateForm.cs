namespace BaGet.Identity.Application.Models.Account;

/// <summary>What the "sign in again" button posts.</summary>
public class ReauthenticateForm
{
    /// <summary>Where to go after the provider answered; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }
}
