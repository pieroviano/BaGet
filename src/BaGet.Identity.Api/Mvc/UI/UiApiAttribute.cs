using System;
using BaGet.Identity.Application.Screens;

namespace BaGet.Identity.Mvc.UI;

/// <summary>
/// Marks a controller action of <c>/api/ui</c> as the JSON twin of a screen: <c>[UiApi(typeof(AdminUsersController), nameof(AdminUsersController.Index))]</c>
/// names the screen by the view controller and the default GET action that serve it (<see cref="Screen"/>), and <see cref="Handler"/> the
/// <c>?handler=</c> it mirrors (null = the screen itself). The attribute is data for the parity and coverage tests and for the OpenAPI
/// document; a twin is never a <c>UiControllerBase</c>, so the route snapshot of the screens (<c>ui-routes.txt</c>) does not see the API.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class UiApiAttribute : Attribute
{
    private const string ControllerSuffix = "Controller";

    /// <param name="controller">The view controller of the screen this action serves (<c>typeof(AdminUsersController)</c>).</param>
    /// <param name="action">The action of that controller that shows the screen (<c>nameof(AdminUsersController.Index)</c>).</param>
    public UiApiAttribute(Type controller, string action)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("A screen names its action.", nameof(action));

        Action = action;
    }

    /// <summary>The view controller of the screen this action serves.</summary>
    public Type Controller { get; }

    /// <summary>The action of <see cref="Controller"/> that shows the screen.</summary>
    public string Action { get; }

    /// <summary>The screen this action serves, as <c>asp-controller</c>/<c>asp-action</c> name it.</summary>
    public ScreenRef Screen
    {
        get
        {
            var name = Controller.Name;

            return new ScreenRef(name.EndsWith(ControllerSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - ControllerSuffix.Length) : name, Action);
        }
    }

    /// <summary>The page handler this action mirrors (<c>Approve</c> for <c>?handler=Approve</c>), or null for the screen itself (GET) or its default POST.</summary>
    public string? Handler { get; set; }
}
