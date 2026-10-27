using System;

namespace BaGet.Web.V3.Api;

/// <summary>
/// Describes one operation of the administration REST API for the OpenAPI document (<see cref="ApiCatalog"/>), the
/// <c>/admin/api</c> page and the UI-coverage audit: what it does, the Razor page that offers the same operation
/// (<see cref="UiPage"/>, the CLAUDE.md rule "every service has a UI counterpart"), the type of its success body and whether it
/// needs the password confirmation that page asks for. Every action of an <see cref="AdminApiController"/> carries one; a test fails
/// for an action without it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ApiOperationAttribute : Attribute
{
    /// <param name="summary">One sentence, imperative ("Create a group").</param>
    /// <param name="uiPage">The Razor page path that offers the operation, such as <c>/Admin/Groups</c>.</param>
    public ApiOperationAttribute(string summary, string uiPage)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        UiPage = uiPage ?? throw new ArgumentNullException(nameof(uiPage));
    }

    /// <summary>What the operation does.</summary>
    public string Summary { get; }

    /// <summary>The Razor page that offers the same operation.</summary>
    public string UiPage { get; }

    /// <summary>
    /// Whether the page asks the administrator's password again for this operation, so the API asks for the
    /// <see cref="AdminApiController.ConfirmHeader"/> header (a global-key caller needs none: the key is the confirmation).
    /// </summary>
    public bool Confirm { get; set; }

    /// <summary>The type of the success body, or null when there is none.</summary>
    public Type? Response { get; set; }

    /// <summary>The success status (200 unless the operation creates, accepts or returns no content).</summary>
    public int Status { get; set; } = 200;

    /// <summary>Whether <see cref="Response"/> is a list inside a <see cref="Paged{T}"/> envelope.</summary>
    public bool PagedList { get; set; }
}
