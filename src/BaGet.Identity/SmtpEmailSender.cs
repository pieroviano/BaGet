using System;
using System.Net;
using System.Threading.Tasks;
using BaGet.Core;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BaGet.Identity;

/// <summary>
/// Sends Identity's emails over SMTP with MailKit. A failure is logged and then
/// rethrown, so a caller can say "could not send" and never reports success.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender<BaGetUser>
{
    private readonly IOptions<EmailOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendConfirmationLinkAsync(BaGetUser user, string email, string confirmationLink)
        => SendAsync(email, "Confirm your email", LinkBody("Confirm your email address:", confirmationLink));

    public Task SendPasswordResetLinkAsync(BaGetUser user, string email, string resetLink)
        => SendAsync(email, "Reset your password", LinkBody("Reset your password:", resetLink));

    public Task SendPasswordResetCodeAsync(BaGetUser user, string email, string resetCode)
        => SendAsync(email, "Reset your password", $"<p>Your password reset code is <b>{WebUtility.HtmlEncode(resetCode)}</b>.</p>");

    internal static string LinkBody(string text, string link)
    {
        var encoded = WebUtility.HtmlEncode(link);

        return $"<p>{WebUtility.HtmlEncode(text)}</p><p><a href=\"{encoded}\">{encoded}</a></p>";
    }

    internal MimeMessage BuildMessage(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.Value.Smtp.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        return message;
    }

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var smtp = _options.Value.Smtp;

        try
        {
            var message = BuildMessage(to, subject, htmlBody);

            using var client = new SmtpClient();
            var security = !smtp.EnableSsl ? SecureSocketOptions.None
                : smtp.Port == 465 ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(smtp.Host, smtp.Port, security);
            if (!string.IsNullOrEmpty(smtp.UserName))
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not send the email \"{Subject}\" through {Host}:{Port}.", subject, smtp.Host, smtp.Port);
            throw;
        }
    }
}
