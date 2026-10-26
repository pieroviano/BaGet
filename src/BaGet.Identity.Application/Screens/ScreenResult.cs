using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BaGet.Identity.Application;

/// <summary>What a use case decided about a screen.</summary>
public enum ScreenStatus
{
    /// <summary>The screen is shown (or the action succeeded and the screen is shown again).</summary>
    Ok,

    /// <summary>The input was refused; the model and <see cref="IScreenResult.Errors"/> say why.</summary>
    Invalid,

    /// <summary>There is no such screen or item (also the answer to a caller who may not know it exists).</summary>
    NotFound,

    /// <summary>The caller is known but may not do this.</summary>
    Forbidden,

    /// <summary>The caller must sign in first.</summary>
    SignIn,

    /// <summary>The action is done; go to <see cref="IScreenResult.Next"/>.</summary>
    Next,
}

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

/// <summary>The model-independent part of a <see cref="ScreenResult{T}"/>, for the code that maps a result to HTTP or to a view.</summary>
public interface IScreenResult
{
    /// <summary>The decision.</summary>
    ScreenStatus Status { get; }

    /// <summary>Field name to messages; the empty name holds the messages of the form as a whole.</summary>
    IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Where to go, when <see cref="Status"/> is <see cref="ScreenStatus.Next"/>.</summary>
    ScreenNext? Next { get; }

    /// <summary>The HTTP status a view renders with when it is not 200 (an upload refused renders its form again with 400).</summary>
    int? StatusCode { get; }

    /// <summary>The model, or null.</summary>
    [JsonIgnore]
    object? ModelObject { get; }
}

/// <summary>
/// What a use case of a screen returns: independent of the programming model that runs it. The JSON API serialises it as it is
/// (<c>status</c>, <c>model</c>, <c>errors</c>, <c>next</c>); the view controllers turn it into a view, an error status or a redirect.
/// A use case holds the rules and never sees <c>HttpContext</c>, <c>ModelState</c> or <c>TempData</c>.
/// </summary>
/// <typeparam name="T">The screen's model (a plain DTO).</typeparam>
public sealed class ScreenResult<T> : IScreenResult
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    private ScreenResult(ScreenStatus status, T? model, IReadOnlyDictionary<string, string[]>? errors, ScreenNext? next, int? statusCode)
    {
        Status = status;
        Model = model;
        Errors = errors ?? NoErrors;
        Next = next;
        StatusCode = statusCode;
    }

    /// <inheritdoc/>
    public ScreenStatus Status { get; }

    /// <summary>The screen's model; null for an answer that has none (not found, sign in, next).</summary>
    public T? Model { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <inheritdoc/>
    public ScreenNext? Next { get; }

    /// <inheritdoc/>
    public int? StatusCode { get; }

    /// <inheritdoc/>
    object? IScreenResult.ModelObject => Model;

    /// <summary>Show the screen.</summary>
    public static ScreenResult<T> Ok(T model, int? statusCode = null)
        => new(ScreenStatus.Ok, model, null, null, statusCode);

    /// <summary>The input was refused: show the screen again with the reasons.</summary>
    public static ScreenResult<T> Invalid(T model, IReadOnlyDictionary<string, string[]> errors, int? statusCode = null)
        => new(ScreenStatus.Invalid, model, errors ?? throw new ArgumentNullException(nameof(errors)), null, statusCode);

    /// <summary>The input was refused with one message about the form as a whole.</summary>
    public static ScreenResult<T> Invalid(T model, string message, int? statusCode = null)
        => Invalid(model, new Dictionary<string, string[]> { [string.Empty] = new[] { message } }, statusCode);

    /// <summary>No such screen or item.</summary>
    public static ScreenResult<T> NotFound() => new(ScreenStatus.NotFound, default, null, null, null);

    /// <summary>The caller may not do this.</summary>
    public static ScreenResult<T> Forbidden() => new(ScreenStatus.Forbidden, default, null, null, null);

    /// <summary>The caller may not do this, and <paramref name="message"/> says why (carried as the form-wide error, <c>""</c>).</summary>
    public static ScreenResult<T> Forbidden(string message)
        => new(ScreenStatus.Forbidden, default, new Dictionary<string, string[]> { [string.Empty] = new[] { message } }, null, null);

    /// <summary>The caller must sign in.</summary>
    public static ScreenResult<T> SignIn() => new(ScreenStatus.SignIn, default, null, null, null);

    /// <summary>The caller must sign in, and <paramref name="message"/> says how (carried as the form-wide error, <c>""</c>).</summary>
    public static ScreenResult<T> SignIn(string message)
        => new(ScreenStatus.SignIn, default, new Dictionary<string, string[]> { [string.Empty] = new[] { message } }, null, null);

    /// <summary>The action is done; continue at <paramref name="next"/>.</summary>
    public static ScreenResult<T> GoTo(ScreenNext next)
        => new(ScreenStatus.Next, default, null, next ?? throw new ArgumentNullException(nameof(next)), null);

    /// <summary>The same decision for a screen of another model (a gate's refusal, which has no model).</summary>
    public static ScreenResult<T> From(IScreenResult refusal)
    {
        if (refusal == null) throw new ArgumentNullException(nameof(refusal));

        return refusal.Status switch
        {
            ScreenStatus.NotFound => NotFound(),
            ScreenStatus.Forbidden => refusal.Errors.Count > 0 ? new(ScreenStatus.Forbidden, default, refusal.Errors, null, null) : Forbidden(),
            ScreenStatus.SignIn => refusal.Errors.Count > 0 ? new(ScreenStatus.SignIn, default, refusal.Errors, null, null) : SignIn(),
            ScreenStatus.Next => GoTo(refusal.Next!),
            _ => throw new ArgumentException("Only an answer without a model can be carried over.", nameof(refusal)),
        };
    }
}
