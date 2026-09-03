using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class BetaKeys(GameDbContext db)
{
    private const string Prefix = "SE";
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    public async Task<BetaKeyDecision> CheckAsync(string? code, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var normalised = Normalise(code);
        if (normalised.Length == 0)
            return BetaKeyDecision.Refused("Beta key is required.");
        if (normalised.Length > 32)
            return BetaKeyDecision.Refused("That beta key is too long.");

        var key = await db.BetaKeys.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == normalised, cancellationToken);
        return Decide(key, nowUtc);
    }

    /// <summary>
    /// Marks a key as spent without saving. The caller saves it with the new account, which is what
    /// makes the consume and account creation one transaction.
    /// </summary>
    public async Task<BetaKeyDecision> RedeemForAccountAsync(
        string? code,
        PlayerAccount account,
        bool isFirstAccount,
        GameOptions options,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!options.Beta.RequireKey || isFirstAccount || account.IsBot)
            return BetaKeyDecision.Allow(null);

        var normalised = Normalise(code);
        if (normalised.Length == 0)
            return BetaKeyDecision.Refused("Beta key is required.");
        if (normalised.Length > 32)
            return BetaKeyDecision.Refused("That beta key is too long.");

        var key = await db.BetaKeys.SingleOrDefaultAsync(x => x.Code == normalised, cancellationToken);
        var decision = Decide(key, nowUtc);
        if (!decision.Accepted || key is null)
            return decision;

        key.Uses += 1;
        key.Version += 1;
        key.RedeemedAtUtc ??= nowUtc;
        if (key.RedeemedByAccountId is null)
        {
            key.RedeemedByAccountId = account.Id;
            key.RedeemedByAccount = account;
        }

        return BetaKeyDecision.Allow(key);
    }

    /// <summary>
    /// Hands a newly made account its own keys to give away.
    ///
    /// Without this the beta cannot spread. Keys existed and could be spent, but nothing ever issued
    /// any except an admin minting them by hand, so the invite chain was one link long: the people an
    /// admin knew, and nobody they knew. A player owning keys is the whole mechanism - they decide who
    /// gets in next.
    ///
    /// Separate from the key they spent getting in. Redeeming somebody else's invite does not touch
    /// the ones handed out here, so arriving and being able to bring three people are the same event.
    ///
    /// Bots get none. They are not people and they have nobody to invite.
    /// </summary>
    public async Task<IReadOnlyList<BetaKey>> GrantToNewAccountAsync(
        PlayerAccount account,
        GameOptions options,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var count = Math.Max(0, options.Beta.KeysPerPlayer);
        if (count <= 0 || account.IsBot)
            return [];

        var expiry = options.Beta.KeyExpiryDays > 0
            ? nowUtc.AddDays(options.Beta.KeyExpiryDays)
            : (DateTime?)null;
        return await MintAsync(count, account.Id, "Invite", maxUses: 1, expiry, cancellationToken);
    }

    /// <summary>
    /// Every account that has never been issued keys of its own, and the keys they should have had.
    ///
    /// The beta gate arrived after people were already playing, so the world it opened on was full of
    /// accounts holding nothing to share - which is the same dead chain as above, just further along.
    /// Matched on having been issued none at all rather than on having none left, so somebody who has
    /// given all of theirs away is not quietly handed a fresh set every time the server restarts.
    /// </summary>
    public async Task<int> BackfillMissingAsync(
        GameOptions options,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var count = Math.Max(0, options.Beta.KeysPerPlayer);
        if (count <= 0)
            return 0;

        var missing = await db.Accounts
            .Where(x => !x.IsBot && !db.BetaKeys.Any(key => key.IssuedToAccountId == x.Id))
            .ToListAsync(cancellationToken);

        foreach (var account in missing)
            await GrantToNewAccountAsync(account, options, nowUtc, cancellationToken);

        if (missing.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    public async Task<IReadOnlyList<BetaKey>> MintAsync(
        int count,
        Guid? issuedToAccountId,
        string? label,
        int maxUses,
        DateTime? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var keys = new List<BetaKey>(Math.Clamp(count, 1, 500));
        for (var i = 0; i < keys.Capacity; i++)
        {
            var code = await NewUniqueCodeAsync(cancellationToken);
            var key = new BetaKey
            {
                Code = code,
                IssuedToAccountId = issuedToAccountId,
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                MaxUses = Math.Max(1, maxUses),
                ExpiresAtUtc = expiresAtUtc,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.BetaKeys.Add(key);
            keys.Add(key);
        }

        return keys;
    }

    internal static string Normalise(string? code)
        => new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    internal static string Display(string code)
    {
        var normalised = Normalise(code);
        return normalised.Length == 12 && normalised.StartsWith(Prefix, StringComparison.Ordinal)
            ? $"{normalised[..2]}-{normalised[2..7]}-{normalised[7..]}"
            : normalised;
    }

    private static BetaKeyDecision Decide(BetaKey? key, DateTime nowUtc)
    {
        if (key is null)
            return BetaKeyDecision.Refused("That beta key does not exist.");
        if (key.RevokedAtUtc is not null)
            return BetaKeyDecision.Refused("That beta key has been revoked.");
        if (key.ExpiresAtUtc is { } expires && expires <= nowUtc)
            return BetaKeyDecision.Refused("That beta key has expired.");
        if (key.Uses >= Math.Max(1, key.MaxUses))
            return BetaKeyDecision.Refused("That beta key has already been used.");

        return BetaKeyDecision.Allow(key);
    }

    private async Task<string> NewUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = Generate();
            if (!await db.BetaKeys.AnyAsync(x => x.Code == code, cancellationToken))
                return code;
        }

        throw new InvalidOperationException("Could not mint a unique beta key.");
    }

    private static string Generate()
    {
        var chars = new char[Prefix.Length + 10];
        Prefix.CopyTo(chars);
        for (var i = Prefix.Length; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}

public sealed record BetaKeyDecision(bool Accepted, string? Error, BetaKey? Key)
{
    public static BetaKeyDecision Allow(BetaKey? key) => new(true, null, key);
    public static BetaKeyDecision Refused(string error) => new(false, error, null);
}
