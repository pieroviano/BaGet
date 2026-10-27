using System;
using BaGet.Identity.Mvc.Attributes;

namespace BaGet.Identity.Mvc.UI;

/// <summary>
/// Marks a controller action of <c>/api/ui</c> as the JSON twin of a screen: <c>[UiApi("/Admin/Users")]</c> names the screen by its page
/// name (the <see cref="LegacyPageAttribute"/> of the view action it serves), and <see cref="Handler"/> the <c>?handler=</c> it mirrors
/// (null = the screen itself). The attribute is data for the parity and coverage tests and for the OpenAPI document; it is never
/// <see cref="LegacyPageAttribute"/>, so the route snapshot of the screens (<c>ui-routes.txt</c>) does not see the API.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class UiApiAttribute : Attribute
{
    /// <param name="screen">The page name of the screen this action serves, as <c>asp-page</c> writes it (<c>/Admin/Users</c>).</param>
    public UiApiAttribute(string screen)
    {
        if (string.IsNullOrWhiteSpace(screen) || screen[0] != '/')
        {
            throw new ArgumentException("A screen name starts with '/', like /Admin/Users.", nameof(screen));
        }

        Screen = screen;
    }

    /// <summary>The screen this action serves (<c>/Admin/Users</c>).</summary>
    public string Screen { get; }

    /// <summary>The page handler this action mirrors (<c>Approve</c> for <c>?handler=Approve</c>), or null for the screen itself (GET) or its default POST.</summary>
    public string? Handler { get; set; }
}
