using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

/// <summary>The "confirm it is you" screen of an account without a password.</summary>
public sealed class ReauthenticateViewModel
{
    /// <summary>Where to go after the provider answered; only a local URL is followed.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>The refusal to show.</summary>
    [BindNever, ValidateNever]
    public string? Error { get; set; }
}
