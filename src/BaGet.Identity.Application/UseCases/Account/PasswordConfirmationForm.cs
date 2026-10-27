using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>What a form that only re-asks the password posts (delete the account, turn two-factor off, reset the authenticator).</summary>
public sealed class PasswordConfirmationForm
{
    /// <summary>The password, asked again.</summary>
    public string? Password { get; set; }
}
