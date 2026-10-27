using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BaGet.Identity.Application.Screens;

/// <summary>Where a finished action leads: the screen (by its page name), its route values and the message to show there.</summary>
/// <param name="Screen">The legacy page name of the next screen (<c>/Admin/Users</c>), as <c>asp-page</c> writes it.</param>
/// <param name="RouteValues">Route and query values of the next screen (<c>repo</c>, <c>name</c>, <c>handler</c>).</param>
/// <param name="Message">The outcome to show on the next screen (a flash message).</param>
/// <param name="Error">The refusal to show on the next screen (a flash error).</param>
/// <param name="LocalUrl">
/// Instead of a screen: a local address to continue at (<c>/packages</c>, or <c>~/</c> for the site root, which the view prefixes with the path
/// base). Always safe to follow: a use case only puts a validated local address here (<see cref="LocalUrls"/>).
/// </param>
/// <param name="Fragment">The URL fragment (<c>#id</c>) to leave the next screen at, or null.</param>
public sealed record ScreenNext(
    string Screen,
    IReadOnlyDictionary<string, object?>? RouteValues = null,
    string? Message = null,
    string? Error = null,
    string? LocalUrl = null,
    string? Fragment = null)
{
    /// <summary>Continue at a local address (a sign-in return URL, the site root).</summary>
    public static ScreenNext Local(string localUrl) => new(string.Empty, LocalUrl: localUrl);
}
