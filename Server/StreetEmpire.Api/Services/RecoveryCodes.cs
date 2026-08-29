using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Making and spending the codes.
///
/// Both halves are here rather than in the endpoints because they are two ends of one decision - how a
/// code is written down decides how it can be looked up - and a mismatch between them is a set of codes
/// that never work, discovered by the one player who needed them.
/// </summary>
public sealed class RecoveryCodes(GameDbContext db, IPasswordHasher<PlayerAccount> hasher)
{
    /// <summary>Ten, which is the number every service that does this landed on for the same reason:
    /// enough to survive losing a few, few enough to print on something.</summary>
    public const int SetSize = 10;

    /// <summary>
    /// Crockford's alphabet, minus the characters that are the same character to somebody reading their
    /// own handwriting back at four in the morning. No I, L, O, U, 0 or 1.
    /// </summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// A fresh set, replacing whatever was there. Returned in the clear once and never again - what is
    /// kept is the hash, so this return value is the only time these strings exist anywhere.
    ///
    /// Replacing rather than adding is the safe way round: somebody making new codes has usually decided
    /// the old sheet is compromised or lost, and a set that quietly kept the old ones valid would not be
    /// the thing they asked for.
    /// </summary>
    public async Task<IReadOnlyList<string>> IssueAsync(PlayerAccount account, CancellationToken cancellationToken)
    {
        // Loaded and removed rather than deleted in the database. ExecuteDelete would be one statement
        // instead of eleven, which for a set that is ten rows by definition is not worth the thing it
        // costs: the in-memory provider the tests use does not implement it, and an issuing path that
        // cannot be tested is the wrong half of that trade.
        var old = await db.RecoveryCodes
            .Where(x => x.AccountId == account.Id)
            .ToListAsync(cancellationToken);
        db.RecoveryCodes.RemoveRange(old);

        var codes = new List<string>(SetSize);
        for (var i = 0; i < SetSize; i++)
        {
            var code = Generate();
            codes.Add(code);
            db.RecoveryCodes.Add(new RecoveryCode
            {
                AccountId = account.Id,
                CodeHash = hasher.HashPassword(account, Normalise(code)),
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return codes;
    }

    /// <summary>How many are left to spend. What the account page reports; never the codes themselves.</summary>
    public Task<int> RemainingAsync(Guid accountId, CancellationToken cancellationToken)
        => db.RecoveryCodes.CountAsync(x => x.AccountId == accountId && x.UsedAtUtc == null, cancellationToken);

    /// <summary>
    /// Spends one, or answers false.
    ///
    /// Every unused code is checked, because the hash is salted per row and there is nothing to look the
    /// typed code up by - which is the cost of storing them like passwords and is why ten is a set and
    /// not a thousand.
    /// </summary>
    public async Task<bool> RedeemAsync(PlayerAccount account, string? code, CancellationToken cancellationToken)
    {
        var typed = Normalise(code);
        if (typed.Length == 0) return false;

        var candidates = await db.RecoveryCodes
            .Where(x => x.AccountId == account.Id && x.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (hasher.VerifyHashedPassword(account, candidate.CodeHash, typed) == PasswordVerificationResult.Failed)
                continue;

            // Spent before the caller does anything with the answer, and in the same save as whatever
            // that is, so a code cannot be used twice by two requests arriving together.
            candidate.UsedAtUtc = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    /// <summary>
    /// What the player typed, made comparable to what was issued: upper case, and the dash and any
    /// stray spaces dropped. Somebody reading a code off paper types it how it looks to them.
    /// </summary>
    internal static string Normalise(string? code)
        => new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// Ten characters in two groups, from a cryptographic source.
    ///
    /// GetInt32 rather than a random byte modulo thirty. The modulo is the version of this that looks
    /// right and is not: thirty does not divide two hundred and fifty-six, so the first sixteen letters
    /// of the alphabet would come up slightly more often than the rest. GetInt32 rejects and redraws to
    /// avoid exactly that, which is the reason to use it rather than do the arithmetic here.
    /// </summary>
    private static string Generate()
    {
        var chars = new char[11];
        for (int i = 0, taken = 0; taken < 10; i++)
        {
            if (i == 5) { chars[i] = '-'; continue; }
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            taken++;
        }
        return new string(chars);
    }
}
