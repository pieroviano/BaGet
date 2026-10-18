namespace BaGet.Core;

/// <summary>
/// Settings of the web site's chrome. BaGet ships no legal text: the operator supplies
/// the Terms of Use and Privacy Policy as Markdown files, and the footer links to them
/// only while they are set.
/// </summary>
public class SiteOptions
{
    /// <summary>
    /// A Markdown file, absolute or relative to the content root, served at /terms.
    /// </summary>
    public string TermsOfUsePath { get; set; }

    /// <summary>
    /// A Markdown file, absolute or relative to the content root, served at /privacy.
    /// </summary>
    public string PrivacyPolicyPath { get; set; }

    /// <summary>
    /// The absolute http or https URL users reach this server at, for example
    /// "https://packages.example.com" (a path is allowed when served under one). Links in
    /// account emails are built only from it. Without it they can only be built from the
    /// request host when "AllowedHosts" restricts the hosts; with the default "*" email is
    /// switched off, because a forged Host header would otherwise decide where a password
    /// reset link points.
    /// </summary>
    public string PublicUrl { get; set; }

    /// <summary>
    /// Show Gravatar avatars. Each avatar is a request to gravatar.com carrying a hash of the
    /// account's email, so a private feed can turn this off and get initial-letter placeholders.
    /// </summary>
    public bool UseGravatar { get; set; } = true;
}
