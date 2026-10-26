using System.Collections.Generic;

namespace BaGet.Web;

/// <summary>One operation of the REST API as the page (and the JSON twin) lists it: flat, with no reflection objects.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path template (<c>/api/v1/groups/{name}</c>).</param>
/// <param name="Summary">What the operation does.</param>
/// <param name="UiPage">The page that offers the same operation (<c>/Admin/Groups</c>).</param>
/// <param name="Confirm">Whether the owner's password is asked (the confirmation header).</param>
public sealed record ApiEndpointSummary(string Method, string Path, string Summary, string UiPage, bool Confirm);

/// <summary>What the REST API page shows (also the model of its JSON twin).</summary>
/// <param name="Endpoints">Every operation, ordered by path.</param>
/// <param name="ApiBase">The address of the API on this server.</param>
/// <param name="ConfirmHeader">The header that carries the password of the owner.</param>
public sealed record ApiCatalogViewModel(IReadOnlyList<ApiEndpointSummary> Endpoints, string ApiBase, string ConfirmHeader)
{
    /// <summary>Where the OpenAPI document is.</summary>
    public string OpenApiUrl => ApiBase + "/openapi.json";
}
