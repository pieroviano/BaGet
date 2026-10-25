using System;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The administration REST API (<c>/api/v1</c>) for people (<c>/admin/api</c>): where it is, how to authenticate (the key of an
/// administrator with the <c>admin:api</c> scope, or the global key), the password confirmation header, and every operation with the
/// page that offers the same operation. Read-only, administrators only. Replaces the <c>/Admin/Api</c> Razor Page
/// (<c>BaGet.Web.Pages.Admin.ApiModel</c> is now an obsolete shim).
/// </summary>
public sealed class ApiCatalogController : AdminControllerBase
{
    private readonly ApiCatalogWorkflow _workflow;

    /// <summary>Creates the controller.</summary>
    public ApiCatalogController(ApiCatalogWorkflow workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    /// <summary>Lists the operations.</summary>
    [Route("admin/api"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Api")]
    public IActionResult Index() => View("Index", _workflow.Build(Request));
}
