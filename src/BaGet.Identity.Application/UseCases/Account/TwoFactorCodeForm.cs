namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>What the "turn two-factor authentication on" form posts.</summary>
public sealed class TwoFactorCodeForm
{
    /// <summary>The code from the authenticator app.</summary>
    public string? Code { get; set; }
}
