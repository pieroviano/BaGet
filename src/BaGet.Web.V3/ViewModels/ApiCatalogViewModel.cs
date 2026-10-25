using System.Collections.Generic;
using BaGet.Web.Api;

namespace BaGet.Web;

/// <summary>What the REST API page shows.</summary>
/// <param name="Endpoints">Every operation, ordered by path.</param>
/// <param name="ApiBase">The address of the API on this server.</param>
/// <param name="ConfirmHeader">The header that carries the password of the owner.</param>
public sealed record ApiCatalogViewModel(IReadOnlyList<ApiEndpoint> Endpoints, string ApiBase, string ConfirmHeader)
{
    /// <summary>Where the OpenAPI document is.</summary>
    public string OpenApiUrl => ApiBase + "/openapi.json";
}
