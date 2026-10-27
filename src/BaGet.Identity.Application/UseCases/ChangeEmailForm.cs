using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application.UseCases;

/// <summary>What the change-email form posts.</summary>
public sealed class ChangeEmailForm
{
    /// <summary>The new address.</summary>
    public string? NewEmail { get; set; }

    /// <summary>The password, asked again (an account without one needs a fresh provider sign-in instead).</summary>
    public string? Password { get; set; }
}
