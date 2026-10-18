using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace BaGet.Tests;

/// <summary>
/// Stands in for an external provider's answer: <c>GET /test/external?sub=&amp;name=&amp;email=&amp;verified=&amp;xsrf=</c>
/// fills the external cookie the way the provider's callback does. <c>xsrf</c> is the value
/// the sign-in challenge was started with (the user id, for a re-authentication).
/// </summary>
internal sealed class ExternalCookieFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, continuation) =>
        {
            if (context.Request.Path != "/test/external")
            {
                await continuation();
                return;
            }

            var query = context.Request.Query;
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, query["sub"]),
                new Claim("preferred_username", query["name"]),
            };
            if (!string.IsNullOrEmpty(query["email"])) claims.Add(new Claim(ClaimTypes.Email, query["email"]));
            if (!string.IsNullOrEmpty(query["verified"])) claims.Add(new Claim("email_verified", query["verified"]));

            var properties = new AuthenticationProperties();
            properties.Items["LoginProvider"] = "oidc";
            if (!string.IsNullOrEmpty(query["xsrf"])) properties.Items["XsrfId"] = query["xsrf"];
            await context.SignInAsync(
                IdentityConstants.ExternalScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
                properties);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        next(app);
    };
}
