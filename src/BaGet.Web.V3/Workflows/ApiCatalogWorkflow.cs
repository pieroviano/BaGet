using System;
using System.Linq;
using BaGet.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BaGet.Web;

/// <summary>
/// Builds the REST API page: the operations are read from the controllers of the API (<see cref="ApiCatalog"/>), the same source as
/// the OpenAPI document, so the list cannot be stale. Used by <see cref="ApiCatalogController"/>.
/// </summary>
public sealed class ApiCatalogWorkflow
{
    private readonly IActionDescriptorCollectionProvider _actions;

    /// <summary>Creates the workflow.</summary>
    public ApiCatalogWorkflow(IActionDescriptorCollectionProvider actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    /// <summary>The page for a request.</summary>
    public ApiCatalogViewModel Build(HttpRequest request)
        => new(
            ApiCatalog.Endpoints(_actions).ToList(),
            $"{request.Scheme}://{request.Host}{request.PathBase}{ApiCatalog.Root}",
            AdminApiController.ConfirmHeader);
}
