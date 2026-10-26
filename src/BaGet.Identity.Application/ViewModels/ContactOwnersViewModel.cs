using System.ComponentModel.DataAnnotations;

namespace BaGet.Identity.ViewModels;

/// <summary>
/// The contact-owners screen (<c>/packages/{id}/contact-owners</c>): the two form fields (<see cref="Message"/>,
/// <see cref="CopyToSelf"/>, bound on POST) and what the screen shows; filled by the screen's workflow.
/// </summary>
public sealed class ContactOwnersViewModel
{
    /// <summary>The longest message the form accepts, in characters.</summary>
    public const int MaxMessageLength = 4000;

    /// <summary>The message to send to the owners.</summary>
    [Required]
    [StringLength(MaxMessageLength)]
    [Display(Name = "Message")]
    public string? Message { get; set; }

    /// <summary>Whether the sender also gets a copy.</summary>
    [Display(Name = "Send me a copy")]
    public bool CopyToSelf { get; set; }

    /// <summary>The package ID as stored, which is the only text that reaches the subject.</summary>
    public string? PackageId { get; internal set; }

    /// <summary>Whether the message was sent.</summary>
    public bool Sent { get; internal set; }

    /// <summary>Why the message was not sent, or null.</summary>
    public string? Error { get; internal set; }

    /// <summary>
    /// Replies come to the sender's address, so there must be one, and it must be confirmed: an unconfirmed address could be anyone's.
    /// </summary>
    public bool SenderHasEmail { get; internal set; }
}

/// <summary>
/// The fields of the contact-owners form as the screen receives them (a page's form post or the JSON body of the twin). It carries no
/// validation attributes: the use case validates by itself (<see cref="ContactOwnersViewModel"/> holds the rules), so a page's model
/// binding does not report the same error a second time.
/// </summary>
public sealed class ContactOwnersForm
{
    /// <summary>The message to send to the owners.</summary>
    public string? Message { get; set; }

    /// <summary>Whether the sender also gets a copy.</summary>
    public bool CopyToSelf { get; set; }
}
