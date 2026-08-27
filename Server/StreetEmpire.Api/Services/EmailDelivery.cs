using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What the server needs before it can send anything.
///
/// Same shape as the Discord settings and for the same reason: the key is blank in the repository and
/// arrives from the gitignored <c>.env</c> file, or from the real environment in production. Until it
/// does, mail is written to the log instead of sent, which keeps the whole flow clickable in
/// development without an account anywhere. See <c>.env.example</c> for the names.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>A Resend API key. Nothing is sent without one.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The from address. Resend will only accept a domain that has been verified with them, which is
    /// the thing that actually keeps these out of spam folders - a raw SMTP server of our own could
    /// not have earned that.
    /// </summary>
    public string FromAddress { get; set; } = "Street Empire <onboarding@resend.dev>";

    /// <summary>Named in the subject line and the body, so a player knows what they are confirming.</summary>
    public string AppName { get; set; } = "Street Empire";

    /// <summary>How long a code is worth typing. Six digits is a small space; the clock is what guards it.</summary>
    public int CodeLifetimeMinutes { get; set; } = 15;

    /// <summary>Wrong guesses before the code is burned and a new one has to be asked for.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait between one code and the next, which is what stops the button being a mail cannon.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Whether registering with an address should put a code in the post straight away.</summary>
    public bool SendOnSignUp { get; set; } = true;

    /// <summary>
    /// Whether a change to a way in is reported to the confirmed address on the account.
    ///
    /// On by default, and there is no good reason to turn it off outside a test: these are the notices
    /// that let somebody notice their account being taken. It exists as a switch because a server
    /// running against a real provider in a load test should not mail a real person a thousand times.
    /// </summary>
    public bool SendSecurityNotices { get; set; } = true;

    /// <summary>
    /// How long a spent or expired code is kept before it is swept.
    ///
    /// They are worth keeping for a few days - "did a code actually go out on Tuesday" is a real
    /// question - and worth nothing after that. A row that can never be used again is a spent secret
    /// sitting in a table, and the table only grows.
    /// </summary>
    public int CodeRetentionDays { get; set; } = 7;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>One message, in both the shapes a mail client might read.</summary>
public sealed record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    /// <summary>Whether this sender actually puts mail on the wire, or only writes it down.</summary>
    bool Delivers { get; }

    /// <summary>False when the message did not go. Callers decide whether that is fatal; mostly it is not.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// Resend's HTTP API.
///
/// An HTTP call rather than SMTP on purpose. Running a mail server means owning deliverability -
/// reverse DNS, SPF, DKIM, DMARC, warming an IP, and a reputation that is lost faster than it is
/// earned - and the reward for all of it is verification mail that lands in spam anyway.
/// </summary>
public sealed class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger) : IEmailSender
{
    private const string Endpoint = "https://api.resend.com/emails";

    public bool Delivers => options.Value.IsConfigured;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                from = settings.FromAddress,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }), Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return true;

            // The body carries Resend's own reason - an unverified domain, a malformed from address -
            // and without it every failure here reads as an unexplained false.
            logger.LogWarning(
                "Resend refused a message with {Status}: {Body}",
                response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not reach Resend.");
            return false;
        }
    }
}

/// <summary>
/// What runs when no API key is set: the message goes to the log instead of the wire.
///
/// This is what makes the whole flow playable in development without an account anywhere. It is also
/// why the account page says out loud which mode it is in - a code sitting in a server log is fine on
/// a laptop and would be a quiet disaster in production, so nothing pretends the mail was sent.
/// </summary>
public sealed class LoggedEmailSender(ILogger<LoggedEmailSender> logger) : IEmailSender
{
    public bool Delivers => false;

    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "No email provider is configured, so this message was not sent.\nTo: {To}\nSubject: {Subject}\n\n{Text}",
            message.To, message.Subject, message.Text);
        return Task.FromResult(true);
    }
}

/// <summary>
/// The message that carries a code.
///
/// One builder for both flows rather than two, so the two cannot drift apart in tone or forget the same
/// line. What changes between them is only what the code is for and what it would mean if the reader
/// did not ask for it - which for a reset is a good deal more serious than for a confirmation.
/// </summary>
public static class CodeEmail
{
    public static EmailMessage Build(
        string to,
        string playerName,
        string code,
        VerificationPurpose purpose,
        int minutes,
        string appName)
    {
        var (what, ifNotYou) = purpose switch
        {
            VerificationPurpose.ResetPassword => (
                $"That is the code to set a new password on your {appName} account, {playerName}.",
                "If you did not ask to reset it, ignore this and nothing happens - your password has not "
                + "changed and this code expires on its own. Somebody knows your username or address, "
                + "though, so it is worth making sure your password is not one you use anywhere else."),
            _ => (
                $"That is the code to confirm this address on your {appName} account, {playerName}.",
                "If you did not ask for it, nothing has happened and you can ignore this."),
        };

        var subject = purpose == VerificationPurpose.ResetPassword
            ? $"{code} is your {appName} password reset code"
            : $"{code} is your {appName} code";

        var text =
            $"""
            {code}

            {what}
            Type it into the {(purpose == VerificationPurpose.ResetPassword ? "sign-in screen" : "Account page")}. It is good for {minutes} minutes.

            {ifNotYou}
            """;

        // Inline styles and a table-free layout: mail clients are not browsers, and half of them will
        // throw away a stylesheet without telling anybody.
        var html =
            $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1a1a1a">
              <p style="margin:0 0 24px;font-size:15px">{Escape(what)}</p>
              <p style="margin:0 0 24px;font-size:34px;font-weight:700;letter-spacing:.18em;font-family:ui-monospace,SFMono-Regular,Menlo,monospace">{Escape(code)}</p>
              <p style="margin:0 0 24px;font-size:15px">It is good for {minutes} minutes.</p>
              <p style="margin:0;font-size:13px;color:#666">{Escape(ifNotYou)}</p>
            </div>
            """;

        return new EmailMessage(to, subject, html, text);
    }

    /// <summary>
    /// The player name is chosen by the player, so it is never dropped into markup as it was typed.
    /// </summary>
    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

