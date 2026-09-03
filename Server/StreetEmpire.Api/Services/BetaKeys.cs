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

        return await MintAsync(count, account.Id, "Invite", maxUses: 1, cancellationToken);
    }

    /// <summary>
    /// Brings every account to the state a member of a closed beta should be in: one key of their own
    /// that they have spent, and a handful they have not, to give away.
    ///
    /// Both halves were wrong, in opposite directions.
    ///
    /// The migration that introduced keys issued one to each existing player and left it unspent, so
    /// the board showed a row of keys attached to people and redeemed by nobody. That reads as a key
    /// somebody has been given and not used, when what it actually means is "this person was here
    /// before the door existed". Marking it spent by the account it belongs to is what makes the
    /// record say the true thing: they are in, on a key of their own.
    ///
    /// And the first pass at issuing shares looked for accounts that had been issued nothing at all -
    /// which is every account except exactly the ones that needed it, because the players who predate
    /// the gate all held that one migration key. They were skipped for having a key they could not
    /// use. This counts what is actually spare instead.
    ///
    /// Idempotent, so it is safe on every boot: an account with an entry key and a hand already dealt
    /// is untouched, and giving keys away never earns replacements.
    /// </summary>
    public async Task<int> EnsureAccountKeysAsync(
        GameOptions options,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var share = Math.Max(0, options.Beta.KeysPerPlayer);
        var accounts = await db.Accounts.Where(x => !x.IsBot).ToListAsync(cancellationToken);
        if (accounts.Count == 0)
            return 0;

        // Two reads for the whole world rather than two per account. A beta is small, and this runs
        // at boot on the thread that has not started serving yet.
        var issued = await db.BetaKeys
            .Where(x => x.IssuedToAccountId != null)
            .ToListAsync(cancellationToken);
        var alreadyIn = (await db.BetaKeys
                .Where(x => x.RedeemedByAccountId != null)
                .Select(x => x.RedeemedByAccountId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var touched = 0;

        foreach (var account in accounts)
        {
            var theirs = issued.Where(x => x.IssuedToAccountId == account.Id).ToList();
            var changed = false;

            if (!alreadyIn.Contains(account.Id))
            {
                // Their own key, spent by them. Prefer one they were already given - that is the
                // migration's key, and using it is what turns the row on the board into the truth -
                // and mint one only for somebody who has none at all.
                var entry = theirs
                    .Where(Spendable)
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefault();
                if (entry is null)
                {
                    entry = (await MintAsync(1, account.Id, "Beta member", maxUses: 1, cancellationToken))[0];
                    theirs.Add(entry);
                }

                entry.Uses = Math.Max(1, entry.Uses);
                entry.RedeemedByAccountId = account.Id;
                entry.RedeemedByAccount = account;
                // Dated when the key existed rather than now, so the board does not claim every player
                // in the world joined during a restart.
                entry.RedeemedAtUtc ??= entry.CreatedAtUtc;
                entry.Version += 1;
                changed = true;
            }

            // Then the hand they can give away - counted on every key they have ever been handed
            // rather than on what is left of it.
            //
            // Counting what is spare would refill the hand the moment they used it, which is an
            // unlimited invite fountain wearing a number. A closed beta is closed: three keys is
            // three people, and giving one away is what it is for.
            var handed = theirs.Count(x => x.RedeemedByAccountId != account.Id);
            if (handed < share)
            {
                await MintAsync(share - handed, account.Id, "Invite", maxUses: 1, cancellationToken);
                changed = true;
            }

            if (changed) touched++;
        }

        if (touched > 0)
            await db.SaveChangesAsync(cancellationToken);
        return touched;

        static bool Spendable(BetaKey key)
            => key.RevokedAtUtc is null && key.RedeemedByAccountId is null && key.Uses < key.MaxUses;
    }

    public async Task<IReadOnlyList<BetaKey>> MintAsync(
        int count,
        Guid? issuedToAccountId,
        string? label,
        int maxUses,
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
