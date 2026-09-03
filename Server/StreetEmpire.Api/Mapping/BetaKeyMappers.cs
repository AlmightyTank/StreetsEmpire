using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Mapping;

internal static class BetaKeyMappers
{
    internal static AccountInviteKeyResponse ToAccountInviteResponse(BetaKey key, DateTime nowUtc)
        => new(
            key.Id,
            key.Code,
            BetaKeys.Display(key.Code),
            key.Label,
            key.MaxUses,
            key.Uses,
            UsesLeft(key),
            Status(key),
            key.RedeemedByAccount?.Player?.Id,
            key.RedeemedByAccount?.Player?.Name ?? key.RedeemedByAccount?.Username,
            key.RedeemedAtUtc,
            key.RevokedAtUtc,
            key.CreatedAtUtc);

    internal static AdminBetaKeyResponse ToAdminResponse(BetaKey key, DateTime nowUtc)
        => new(
            key.Id,
            key.Code,
            BetaKeys.Display(key.Code),
            key.Label,
            key.MaxUses,
            key.Uses,
            UsesLeft(key),
            Status(key),
            key.IssuedToAccountId,
            key.IssuedToAccount?.Player?.Id,
            key.IssuedToAccount?.Player?.Name,
            key.IssuedToAccount?.Username,
            key.RedeemedByAccountId,
            key.RedeemedByAccount?.Player?.Id,
            key.RedeemedByAccount?.Player?.Name,
            key.RedeemedByAccount?.Username,
            key.RedeemedAtUtc,
            key.RevokedAtUtc,
            key.CreatedAtUtc);

    private static int UsesLeft(BetaKey key)
        => Math.Max(0, Math.Max(1, key.MaxUses) - key.Uses);

    /// <summary>
    /// Three states, and there is no fourth. A key is taken back, spent, or waiting - it does not go
    /// off on its own while nobody is looking.
    /// </summary>
    private static string Status(BetaKey key)
    {
        if (key.RevokedAtUtc is not null) return "Revoked";
        if (key.Uses >= Math.Max(1, key.MaxUses)) return "Used";
        return "Available";
    }
}
