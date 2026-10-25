using System;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the access-unavailable page when it was a Razor Page: where the access pages send an administrator when the
/// configured database has no access model (Azure Table).
/// </summary>
/// <remarks>Kept as a shim (no breaking change of the public API); <c>AdminOperationsController.AccessUnavailable</c> serves the page.</remarks>
[Obsolete("The access-unavailable screen is served by AdminOperationsController; this page model has no page any more and will be removed in the next major version.")]
public class AccessUnavailableModel : AdminPageModel
{
}
