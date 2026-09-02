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
            Status(key, nowUtc),
            key.RedeemedByAccount?.Player?.Id,
            key.RedeemedByAccount?.Player?.Name ?? key.RedeemedByAccount?.Username,
            key.RedeemedAtUtc,
            key.ExpiresAtUtc,
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
            Status(key, nowUtc),
            key.IssuedToAccountId,
            key.IssuedToAccount?.Player?.Id,
            key.IssuedToAccount?.Player?.Name,
            key.IssuedToAccount?.Username,
            key.RedeemedByAccountId,
            key.RedeemedByAccount?.Player?.Id,
            key.RedeemedByAccount?.Player?.Name,
            key.RedeemedByAccount?.Username,
            key.RedeemedAtUtc,
            key.ExpiresAtUtc,
            key.RevokedAtUtc,
            key.CreatedAtUtc);

    private static int UsesLeft(BetaKey key)
        => Math.Max(0, Math.Max(1, key.MaxUses) - key.Uses);

    private static string Status(BetaKey key, DateTime nowUtc)
    {
        if (key.RevokedAtUtc is not null) return "Revoked";
        if (key.ExpiresAtUtc is { } expires && expires <= nowUtc) return "Expired";
        if (key.Uses >= Math.Max(1, key.MaxUses)) return "Used";
        return "Available";
    }
}
