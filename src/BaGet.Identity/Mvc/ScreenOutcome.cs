using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Mvc;

/// <summary>
/// What an account workflow decided, independent of the programming model that runs it: show the screen again, or leave for
/// somewhere. <see cref="ToResult"/> turns it into the same <see cref="IActionResult"/> a controller action returns, so the workflow holds the rules and the host holds only the plumbing.
/// </summary>
internal sealed class ScreenOutcome
{
    private enum Kind { Show, NotFound, Challenge, ChallengeWith, Local, LocalExact, Page, Status }

    private readonly Kind _kind;
    private readonly string? _text;
    private readonly object? _values;
    private readonly AuthenticationProperties? _properties;
    private readonly int _status;

    private ScreenOutcome(Kind kind, string? text = null, object? values = null, AuthenticationProperties? properties = null, int status = 0)
    {
        _kind = kind;
        _text = text;
        _values = values;
        _properties = properties;
        _status = status;
    }

    /// <summary>Render the screen (<c>Page()</c> / <c>View(model)</c>).</summary>
    public static readonly ScreenOutcome Show = new(Kind.Show);

    /// <summary>404.</summary>
    public static readonly ScreenOutcome NotFound = new(Kind.NotFound);

    /// <summary>Send the visitor to sign in.</summary>
    public static readonly ScreenOutcome Challenge = new(Kind.Challenge);

    /// <summary>Send the visitor to an external provider.</summary>
    public static ScreenOutcome ChallengeWith(string scheme, AuthenticationProperties properties)
        => new(Kind.ChallengeWith, scheme, properties: properties);

    /// <summary>A local redirect to <paramref name="returnUrl"/> when it is local, else to the site root.</summary>
    public static ScreenOutcome Local(string? returnUrl) => new(Kind.Local, returnUrl);

    /// <summary>A local redirect to exactly <paramref name="url"/> (which must be local: an application-relative address).</summary>
    public static ScreenOutcome LocalExact(string url) => new(Kind.LocalExact, url);

    /// <summary>A redirect to another page, by its page name (<c>/Account/Login</c>).</summary>
    public static ScreenOutcome ToPage(string page, object? values = null) => new(Kind.Page, page, values);

    /// <summary>A bare status code.</summary>
    public static ScreenOutcome Status(int code) => new(Kind.Status, status: code);

    /// <summary>Turns the decision into an action result; <paramref name="show"/> renders the screen.</summary>
    public IActionResult ToResult(IUrlHelper url, Func<IActionResult> show)
    {
        switch (_kind)
        {
            case Kind.Show: return show();
            case Kind.NotFound: return new NotFoundResult();
            case Kind.Challenge: return new ChallengeResult();
            case Kind.ChallengeWith: return new ChallengeResult(_text!, _properties);
            case Kind.Local: return new LocalRedirectResult(AccountSupport.LocalTarget(url, _text)) { UrlHelper = url };
            case Kind.LocalExact: return new LocalRedirectResult(_text!) { UrlHelper = url };
            case Kind.Page: return new RedirectToPageResult(_text, _values) { UrlHelper = url };
            case Kind.Status: return new StatusCodeResult(_status);
            default: throw new InvalidOperationException();
        }
    }
}
