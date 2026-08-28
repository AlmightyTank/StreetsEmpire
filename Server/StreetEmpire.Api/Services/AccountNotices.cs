using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The account changes worth telling somebody about.
///
/// The test for being on this list is not "did something change" but "would the owner want to know if
/// it was not them". Anything that moves a way in, or that would let somebody keep the account after
/// taking it, qualifies. Anything a player does to their empire does not - the activity log is where
/// that belongs, and a notice for every action is a notice nobody reads.
/// </summary>
public enum AccountChange
{
    PasswordSet,
    PasswordChanged,
    /// <summary>Changed by somebody who proved they can read the account's mail, without the old one.</summary>
    PasswordReset,
    /// <summary>
    /// Not a settings change, and on the list anyway. A connected Discord signs in without a password,
    /// so this is the one event that reports somebody simply being in the account - which is the thing
    /// a person most wants to hear about and the last thing anything else would tell them.
    /// </summary>
    SignedInWithDiscord,
    EmailChanged,
    EmailRemoved,
    DiscordConnected,
    DiscordDisconnected,
    SessionsSignedOut,
}

/// <summary>
/// Telling a player that their account changed.
///
/// The point of these is the case where the change was not theirs. A password quietly changed by
/// somebody holding a borrowed session is invisible until the day the real owner tries to sign in;
/// a notice makes it visible within a minute, which is the difference between an account that can be
/// saved and one that is gone.
///
/// Nothing here ever fails the change it is reporting. The account has already been altered and saved
/// by the time a notice is attempted - a provider being down must not roll that back, and must not
/// answer an error to a player whose password really did change.
/// </summary>
public sealed class AccountNotices(
    IEmailSender sender,
    IOptions<EmailOptions> options,
    ILogger<AccountNotices> logger)
{
    /// <summary>
    /// Tells the account's own confirmed address, and does nothing when there is not one.
    ///
    /// Confirmed only, deliberately. An unconfirmed address may belong to a stranger who was typed in
    /// by accident or on purpose, and mailing them about an account that is not theirs is both a
    /// nuisance to them and a spam complaint against the sending domain - which would eventually stop
    /// the verification codes arriving for everybody. The cost is that an account whose address was
    /// never confirmed gets no notices at all, which is one more reason the account page pushes to
    /// confirm.
    /// </summary>
    public Task TellAccountAsync(PlayerAccount account, AccountChange change, string? detail, CancellationToken ct)
        => account is { EmailVerified: true, Email: not null, EmailSecurityNotices: true }
            ? SendAsync(account.Email, account.Player?.Name ?? account.Username, change, detail, ct)
            : Task.CompletedTask;

    /// <summary>
    /// Tells an address the account is about to stop pointing at.
    ///
    /// This is the one that matters most. Changing the address is how somebody who has taken an account
    /// keeps it - the old owner is cut off and never told - so the notice has to go to where the account
    /// used to point, at the moment it stops pointing there. Telling only the new address would be
    /// telling the thief.
    /// </summary>
    public Task TellFormerAddressAsync(string address, string playerName, AccountChange change, string? detail, CancellationToken ct)
        => SendAsync(address, playerName, change, detail, ct);

    private async Task SendAsync(string to, string playerName, AccountChange change, string? detail, CancellationToken ct)
    {
        if (!options.Value.SendSecurityNotices) return;

        try
        {
            var message = AccountNoticeEmail.Build(to, playerName, change, detail, DateTime.UtcNow, options.Value.AppName);
            if (!await sender.SendAsync(message, ct))
                logger.LogWarning("Could not send a {Change} notice.", change);
        }
        catch (Exception ex)
        {
            // Belt and braces over the sender's own handling. A notice is the least important thing in
            // the request that triggered it, and must never be the reason one fails.
            logger.LogWarning(ex, "Could not send a {Change} notice.", change);
        }
    }
}

/// <summary>
/// The copy.
///
/// One rule runs through all of it: a notice says what happened and never carries the thing that
/// happened. No new password, no verification code, no Discord token. A mailbox is not a secure
/// channel, and a notice that leaks the change is worse than no notice at all.
/// </summary>
public static class AccountNoticeEmail
{
    public static EmailMessage Build(
        string to,
        string playerName,
        AccountChange change,
        string? detail,
        DateTime whenUtc,
        string appName)
    {
        var (subject, happened) = Describe(change, detail);
        var when = whenUtc.ToString("HH:mm 'UTC' on d MMMM yyyy");

        // Written for the reader who has already lost the account, because that is who most needs it.
        // "Sign in and change your password" is useless advice to somebody whose password was just
        // changed out from under them - so the route named is the one that works without it, which is
        // the reset, and the reset needs only this mailbox.
        const string ifNotYou =
            "If that was not you, take the account back now: reset your password from the sign-in screen "
            + "using this address, which signs out every other session. Then check the Sign-in tab for a "
            + "Discord connection you do not recognise.";

        var text =
            $"""
            {happened}

            {when}

            {ifNotYou}
            """;

        var html =
            $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1a1a1a">
              <p style="margin:0 0 8px;font-size:15px">{Escape(happened)}</p>
              <p style="margin:0 0 24px;font-size:13px;color:#666">{Escape(when)}</p>
              <p style="margin:0;font-size:14px">{Escape(ifNotYou)}</p>
            </div>
            """;

        return new EmailMessage(to, $"{appName}: {subject}", html, text);
    }

    /// <param name="detail">
    /// Context the sentence needs and the enum cannot carry - which Discord handle, which address the
    /// account moved to. Never a secret.
    /// </param>
    private static (string Subject, string Happened) Describe(AccountChange change, string? detail) => change switch
    {
        AccountChange.PasswordSet => (
            "a password was set on your account",
            "A password was set on your account. It can now be signed in with, alongside anything else that was already connected."),

        AccountChange.PasswordChanged => (
            "your password was changed",
            "The password on your account was changed. Every other session was signed out."),

        AccountChange.PasswordReset => (
            "your password was reset",
            "Your password was reset using a code sent to this address, and every other session was signed out. "
            + "Whoever did it did not need the old password - only this mailbox."),

        AccountChange.SignedInWithDiscord => (
            "a Discord account signed in",
            detail is null
                ? "A connected Discord account signed into your empire."
                : $"The Discord account {detail} signed into your empire."),

        // Named rather than hinted at. This notice goes to the address being left behind, and its whole
        // job is to let somebody see where their account went.
        AccountChange.EmailChanged => (
            "your email address was changed",
            detail is null
                ? "The email address on your account was changed away from this one. This address can no longer be used to sign in."
                : $"The email address on your account was changed from this one to {detail}. This address can no longer be used to sign in."),

        AccountChange.EmailRemoved => (
            "your email address was removed",
            "The email address was removed from your account. This address can no longer be used to sign in, and no further notices will be sent to it."),

        AccountChange.DiscordConnected => (
            "a Discord account was connected",
            detail is null
                ? "A Discord account was connected to your account. It can now sign in without a password."
                : $"The Discord account {detail} was connected to your account. It can now sign in without a password."),

        AccountChange.DiscordDisconnected => (
            "a Discord account was disconnected",
            detail is null
                ? "A Discord account was disconnected from your account. It can no longer sign in."
                : $"The Discord account {detail} was disconnected from your account. It can no longer sign in."),

        AccountChange.SessionsSignedOut => (
            "you were signed out everywhere",
            "Every session on your account except one was signed out."),

        _ => ("your account changed", "Something on your account changed."),
    };

    /// <summary>
    /// Both the player name and the detail are things a person chose, so neither reaches the body as
    /// markup. A Discord handle in particular is somebody else's text entirely.
    /// </summary>
    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
