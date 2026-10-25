using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BaGet.Web.Api;

/// <summary>
/// The OpenAPI 3.0 description of the administration API, generated from its controllers (<see cref="ApiCatalog"/>), so it lists
/// exactly the operations the server has. Needs the same credentials as the API itself; the <c>/admin/api</c> page shows the same list
/// to a signed-in administrator.
/// </summary>
[Route(ApiCatalog.RootTemplate)]
public class OpenApiController : AdminApiController
{
    /// <summary>The version of the API (the path says it too).</summary>
    public const string ApiVersion = "1.0.0";

    private readonly IActionDescriptorCollectionProvider _actions;

    /// <summary>Creates the controller.</summary>
    public OpenApiController(IActionDescriptorCollectionProvider actions)
    {
        _actions = actions;
    }

    /// <summary>The document.</summary>
    [HttpGet("openapi.json")]
    [ApiOperation("Download the OpenAPI 3 description of this API", "/Admin/Api")]
    public IActionResult Document()
    {
        var root = Service<IUrlGenerator>().GetServiceIndexUrl() ?? string.Empty;
        const string index = "v3/index.json";
        var server = root.EndsWith(index, System.StringComparison.Ordinal) ? root.Substring(0, root.Length - index.Length) : root.TrimEnd('/') + "/";
        var document = ApiCatalog.OpenApi(ApiCatalog.Endpoints(_actions), server.TrimEnd('/'), ApiVersion);

        return new ContentResult { Content = document.ToJsonString(), ContentType = "application/json", StatusCode = 200 };
    }
}
