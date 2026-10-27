using BaGet.Identity.Application.Email;

namespace BaGet.Identity.Application.Screens;

/// <summary>
/// Builds the address of a screen for a use case that has to put one in a message (a password reset link in an email). The implementation
/// belongs to the UI project, which knows the routes (the legacy page names of the screens); a use case never sees an URL helper.
/// </summary>
public interface IScreenLinks
{
    /// <summary>
    /// An absolute link to a screen, for an email. Built only from <c>Site:PublicUrl</c>, never from the request's host or scheme (see
    /// <see cref="EmailLinkPolicy"/>).
    /// </summary>
    /// <param name="page">The screen's page name, as <c>asp-page</c> writes it (<c>/Account/ResetPassword</c>).</param>
    /// <param name="values">Route values, e.g. <see cref="PasswordResetLinkSender.Template"/>.</param>
    /// <exception cref="System.InvalidOperationException"><c>Site:PublicUrl</c> is not set to a usable address.</exception>
    string AbsoluteLink(string page, object values);
}
