using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application.UseCases;

/// <summary>Where a browser is sent to talk to the external provider.</summary>
/// <param name="Scheme">The authentication scheme to challenge.</param>
/// <param name="Properties">The challenge's properties (callback address, anti-forgery value, prompt).</param>
public sealed record ExternalChallenge(string Scheme, AuthenticationProperties Properties);
