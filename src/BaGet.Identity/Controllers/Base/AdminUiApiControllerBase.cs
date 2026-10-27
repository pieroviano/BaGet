using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Mvc;
using BaGet.Identity.Mvc.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers.Base;

/// <summary>
/// Base of the administrator screens' JSON twins: the caller is an administrator's API key with the <c>admin:api</c> scope and no repository limit
/// (see <see cref="ScreenGate.IsAdministratorKey"/>): 401 with no key, 403 for any other key, the global <c>ApiKey</c> included.
/// </summary>
public abstract class AdminUiApiControllerBase : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <inheritdoc/>
    protected override bool CookieOnly => false;

    /// <inheritdoc/>
    protected override GateRequest Requirements(ActionExecutingContext context)
        => base.Requirements(context) with { Role = GateRole.Administrator };
}
