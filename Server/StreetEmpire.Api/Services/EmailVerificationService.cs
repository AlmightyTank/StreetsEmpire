using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>What happened when a code was asked for.</summary>
public enum SendCodeResult
{
    Sent,
    /// <summary>Asked again too soon. The cooldown is what stops the button being a mail cannon.</summary>
    TooSoon,
    /// <summary>There is no address on the account to send anything to.</summary>
    NoAddress,
    /// <summary>Already ticked. Sending another code would only invite somebody to type one.</summary>
    AlreadyVerified,
    /// <summary>
    /// A reset was asked for against an address nobody has proved. Refused, because a reset code sent
    /// to an unproven address is a way into the account handed to whoever typed the address in.
    /// </summary>
    AddressNotConfirmed,
    /// <summary>
    /// This address has had its day's worth. Distinct from TooSoon because the answer is different:
    /// waiting a minute will not help.
    /// </summary>
    TooMany,
    /// <summary>The provider would not take it. The code exists; the message did not go.</summary>
    NotDelivered,
}

/// <summary>What happened when a code was typed in.</summary>
public enum ConfirmCodeResult
{
    Verified,
    /// <summary>Wrong, and there are guesses left.</summary>
    Wrong,
    /// <summary>Out of time, out of guesses, or already spent. All of them mean: ask for a new one.</summary>
    Expired,
    /// <summary>Nothing outstanding to confirm.</summary>
    NothingToConfirm,
    AlreadyVerified,
}

/// <summary>
/// Issuing and checking the six digits, for both of the things they are used for.
///
/// The whole design rests on one trade. Six digits is a million possibilities, which is small enough
/// to guess through if you are given time and tries - so the code is given neither. It lives fifteen
/// minutes, it takes five wrong guesses before it is burned, and a new one cannot be asked for more
/// than once a minute. A longer window would need a longer secret, which would need a link rather than
/// something a person types, which is the flow this deliberately is not.
///
/// Confirming an address and resetting a password are the same machinery with opposite preconditions:
/// one needs an address nobody has proved, the other needs one somebody already did. They are told
/// apart by the purpose on the row, and a code issued for one is never accepted by the other.
/// </summary>
public sealed class EmailVerificationService(
    GameDbContext db,
    IEmailSender sender,
    IDataProtectionProvider protection,
    IOptions<EmailOptions> options,
    ILogger<EmailVerificationService> logger)
{
    private readonly IDataProtector _seal = protection.CreateProtector("StreetEmpire.EmailVerification");

    public EmailOptions Options => options.Value;

    /// <summary>Whether mail actually leaves the building, or only reaches the log.</summary>
    public bool Delivers => sender.Delivers;

    /// <summary>
    /// Issues a code and sends it, retiring whatever was outstanding for the same purpose.
    ///
    /// The previous code is consumed rather than left alongside the new one. Two live codes means two
    /// chances to guess and, worse, a player typing the older of two mails and being told they are
    /// wrong.
    /// </summary>
    public async Task<SendCodeResult> SendAsync(PlayerAccount account, VerificationPurpose purpose, CancellationToken ct)
    {
        if (account.Email is null) return SendCodeResult.NoAddress;

        // The preconditions are exact opposites, which is the whole reason the two flows are told apart
        // rather than sharing one entry point that guesses.
        switch (purpose)
        {
            case VerificationPurpose.ConfirmAddress when account.EmailVerified:
                return SendCodeResult.AlreadyVerified;
            case VerificationPurpose.ResetPassword when !account.EmailVerified:
                return SendCodeResult.AddressNotConfirmed;
        }

        var now = DateTime.UtcNow;
        var outstanding = await LatestAsync(account.Id, purpose, ct);

        if (IsTooSoon(outstanding, account.Email, now, Options.ResendCooldownSeconds))
            return SendCodeResult.TooSoon;

        // The ceiling under the rate. Counted per address rather than per account, for the same reason
        // the cooldown is: the inbox is the thing being protected, and both purposes land in it.
        var since = now.AddDays(-1);
        var today = await db.EmailVerifications
            .CountAsync(x => x.AccountId == account.Id && x.Email == account.Email && x.CreatedAtUtc > since, ct);
        if (today >= Math.Max(1, Options.MaxCodesPerDay))
        {
            logger.LogWarning(
                "Account {Account} has had {Count} code(s) in a day; refusing more until the window moves.",
                account.Id, today);
            return SendCodeResult.TooMany;
        }

        if (outstanding is not null && outstanding.ConsumedAtUtc is null)
            outstanding.ConsumedAtUtc = now;

        var code = NewCode();
        var record = new EmailVerification
        {
            AccountId = account.Id,
            Email = account.Email,
            Purpose = purpose,
            SealedCode = _seal.Protect(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(Options.CodeLifetimeMinutes),
        };
        db.EmailVerifications.Add(record);
        await db.SaveChangesAsync(ct);

        var playerName = account.Player?.Name ?? account.Username;
        var message = CodeEmail.Build(account.Email, playerName, code, purpose, Options.CodeLifetimeMinutes, Options.AppName);
        if (await sender.SendAsync(message, ct))
            return SendCodeResult.Sent;

        // The record stays. A player who is told the mail failed and then finds it in their inbox a
        // minute later should still be able to use what is in it.
        logger.LogWarning("A {Purpose} code was issued for account {Account} but could not be sent.", purpose, account.Id);
        return SendCodeResult.NotDelivered;
    }

    /// <summary>
    /// Checks a typed code against the outstanding one for that purpose.
    ///
    /// Confirming an address ticks the account off here, because that is the whole of what it does. A
    /// reset does not: it only says the code was right, and setting the password is the caller's to do
    /// alongside ending the sessions and sending the notice, in one place where those three cannot
    /// come apart.
    /// </summary>
    public async Task<ConfirmCodeResult> ConfirmAsync(
        PlayerAccount account,
        VerificationPurpose purpose,
        string? typed,
        CancellationToken ct)
    {
        if (purpose == VerificationPurpose.ConfirmAddress && account.EmailVerified)
            return ConfirmCodeResult.AlreadyVerified;
        if (account.Email is null) return ConfirmCodeResult.NothingToConfirm;

        var now = DateTime.UtcNow;
        var record = await LatestAsync(account.Id, purpose, ct);
        if (record is null) return ConfirmCodeResult.NothingToConfirm;
        if (!record.IsLive(now, Options.MaxAttempts)) return ConfirmCodeResult.Expired;

        // The code proves control of the address it was sent to. If the account has been pointed
        // somewhere else since, that proof is about the old address and is worth nothing here.
        if (!string.Equals(record.Email, account.Email, StringComparison.Ordinal))
        {
            record.ConsumedAtUtc = now;
            await db.SaveChangesAsync(ct);
            return ConfirmCodeResult.NothingToConfirm;
        }

        // Counted before the comparison, so a guess costs a try whatever happens next - including a
        // request that gives up half way through.
        record.Attempts++;

        if (!Matches(record.SealedCode, typed))
        {
            var burned = record.Attempts >= Options.MaxAttempts;
            if (burned) record.ConsumedAtUtc = now;
            await db.SaveChangesAsync(ct);
            return burned ? ConfirmCodeResult.Expired : ConfirmCodeResult.Wrong;
        }

        record.ConsumedAtUtc = now;
        if (purpose == VerificationPurpose.ConfirmAddress)
        {
            account.EmailVerified = true;
            account.EmailVerifiedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
        return ConfirmCodeResult.Verified;
    }

    /// <summary>The outstanding code for a purpose, if there is one worth typing against.</summary>
    public async Task<EmailVerification?> PendingAsync(Guid accountId, VerificationPurpose purpose, CancellationToken ct)
    {
        var record = await LatestAsync(accountId, purpose, ct);
        return record is not null && record.IsLive(DateTime.UtcNow, Options.MaxAttempts) ? record : null;
    }

    /// <summary>When the button becomes pressable again, or null if it already is.</summary>
    public async Task<DateTime?> ResendableAtAsync(
        Guid accountId,
        VerificationPurpose purpose,
        string? sendingTo,
        CancellationToken ct)
    {
        var record = await LatestAsync(accountId, purpose, ct);
        // Asks the same question the send does, so the clock on the button can never disagree with
        // what pressing it would actually do.
        return IsTooSoon(record, sendingTo, DateTime.UtcNow, Options.ResendCooldownSeconds)
            ? record!.CreatedAtUtc.AddSeconds(Options.ResendCooldownSeconds)
            : null;
    }

    /// <summary>
    /// Whether a code may not be sent yet.
    ///
    /// Measured from the last code that went out rather than the last one typed, because it is the
    /// sending that costs somebody an inbox - and measured per address, not per account, because that
    /// inbox is the thing being protected.
    ///
    /// The per-address part is not a nicety. A player who changes their address inside the first minute
    /// was refused a code by an account-wide cooldown and told nothing, which left them on a new
    /// address they had no way to confirm and no explanation of why. Changing address is not what the
    /// cooldown is for; hammering resend at one inbox is.
    /// </summary>
    internal static bool IsTooSoon(EmailVerification? latest, string? sendingTo, DateTime nowUtc, int cooldownSeconds)
        => latest is not null
            && string.Equals(latest.Email, sendingTo, StringComparison.Ordinal)
            && latest.CreatedAtUtc.AddSeconds(cooldownSeconds) > nowUtc;

    private Task<EmailVerification?> LatestAsync(Guid accountId, VerificationPurpose purpose, CancellationToken ct)
        => db.EmailVerifications
            .Where(x => x.AccountId == accountId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Uniform across all million values, and from the cryptographic generator rather than Random.
    /// A code that can be predicted from the last one is not a secret, it is a sequence.
    /// </summary>
    private static string NewCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>
    /// Unsealed and compared in fixed time. The timing of a comparison against six digits is not much
    /// of a leak, but it is a free one to close.
    /// </summary>
    private bool Matches(string sealedCode, string? typed)
    {
        var cleaned = new string((typed ?? string.Empty).Where(char.IsDigit).ToArray());
        if (cleaned.Length != 6) return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(_seal.Unprotect(sealedCode)),
                System.Text.Encoding.UTF8.GetBytes(cleaned));
        }
        catch (CryptographicException)
        {
            // Written under a key ring this server no longer has. Unusable, which is a wrong guess.
            return false;
        }
    }
}
