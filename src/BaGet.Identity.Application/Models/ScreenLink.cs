using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>A screen as a client links to it: the page name (as <c>asp-page</c> writes it) and its route values; the view turns it into an address.</summary>
/// <param name="Screen">The legacy page name of the screen (<c>/Package</c>).</param>
/// <param name="RouteValues">Route values; a null value (<c>repo</c> of the default repository) is kept.</param>
public sealed record ScreenLink(string Screen, IReadOnlyDictionary<string, object?> RouteValues);
