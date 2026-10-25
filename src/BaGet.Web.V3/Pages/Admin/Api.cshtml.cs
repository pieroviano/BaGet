using System.Collections.Generic;
using System.Linq;
using BaGet.Identity.Pages.Admin;
using BaGet.Web.Api;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web.Pages.Admin;

/// <summary>
/// The administration REST API (<c>/api/v1</c>) for people: where it is, how to authenticate (an administrator's key with the
/// <c>admin:api</c> scope, or the global key), the password confirmation header, and the list of every operation with the page that offers
/// the same operation. The list is read from the API's controllers (<see cref="ApiCatalog"/>), the same source as the OpenAPI document,
/// so it cannot be stale. Read-only, administrators only.
/// </summary>
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
        => Endpoints = ApiCatalog.Endpoints(HttpContext.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>()).ToList();
}
