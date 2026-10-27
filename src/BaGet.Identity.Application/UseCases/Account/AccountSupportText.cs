using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>The identity failure text shared by the administration use cases.</summary>
internal static class AccountSupportText
{
    /// <summary>The messages of an identity failure as one sentence.</summary>
    public static string Describe(IdentityResult result)
        => string.Join(" ", System.Linq.Enumerable.Select(result.Errors, e => e.Description));
}
