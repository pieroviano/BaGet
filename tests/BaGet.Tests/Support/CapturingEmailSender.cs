using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;

namespace BaGet.Tests;

/// <summary>
/// Stands in for SMTP in the integration harness: records what would have been sent so
/// a test can assert on it and follow the link in it.
/// </summary>
public class CapturingEmailSender : IEmailSender<BaGetUser>
{
    private readonly ConcurrentQueue<CapturedEmail> _sent = new();

    public IReadOnlyCollection<CapturedEmail> Sent => _sent.ToArray();

    public Task SendConfirmationLinkAsync(BaGetUser user, string email, string confirmationLink)
        => Capture("confirmation", user, email, confirmationLink);

    public Task SendPasswordResetLinkAsync(BaGetUser user, string email, string resetLink)
        => Capture("reset-link", user, email, resetLink);

    public Task SendPasswordResetCodeAsync(BaGetUser user, string email, string resetCode)
        => Capture("reset-code", user, email, resetCode);

    private Task Capture(string kind, BaGetUser user, string email, string payload)
    {
        _sent.Enqueue(new CapturedEmail(kind, user?.UserName, email, payload));
        return Task.CompletedTask;
    }
}

public record CapturedEmail(string Kind, string UserName, string To, string Payload);
