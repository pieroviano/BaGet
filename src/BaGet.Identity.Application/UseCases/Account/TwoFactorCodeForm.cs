using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using QRCoder;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>What the "turn two-factor authentication on" form posts.</summary>
public sealed class TwoFactorCodeForm
{
    /// <summary>The code from the authenticator app.</summary>
    public string? Code { get; set; }
}
