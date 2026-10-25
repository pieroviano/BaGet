using System;
using System.Collections.Generic;
using BaGet.Identity.Pages.Admin;
using BaGet.Web.Api;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web.Pages.Admin;

/// <summary>The page model of the REST API page when it was a Razor Page.</summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="ApiCatalogController"/> serves <c>/admin/api</c>. Both share <see cref="ApiCatalogWorkflow"/>.
/// </remarks>
[Obsolete("The REST API page is served by ApiCatalogController; this page model has no page any more and will be removed in the next major version.")]
public class ApiModel : AdminPageModel
{
    /// <summary>Every operation, ordered by path.</summary>
    public IReadOnlyList<ApiEndpoint> Endpoints { get; private set; } = new List<ApiEndpoint>();

    /// <summary>Where the OpenAPI document is.</summary>
    public string OpenApiUrl => ApiBase + "/openapi.json";

    /// <summary>The address of the API on this server.</summary>
    public string ApiBase => $"{Request.Scheme}://{Request.Host}{Request.PathBase}{ApiCatalog.Root}";

    /// <summary>The header that carries the owner's password.</summary>
    public string ConfirmHeader => AdminApiController.ConfirmHeader;

    /// <summary>Lists the operations.</summary>
    public void OnGet()
        => Endpoints = new ApiCatalogWorkflow(HttpContext.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>())
            .Build(Request).Endpoints;
}
