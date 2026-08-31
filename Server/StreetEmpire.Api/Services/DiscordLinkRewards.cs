using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

internal sealed record DiscordLinkRewardResult(bool Awarded, long Cash, int Condoms, int Beer);

internal static class DiscordLinkRewards
{
    internal const long Cash = 10_000;
    internal const int Condoms = 25;
    internal const int Beer = 25;

    internal static DiscordLinkRewardResult GrantOnce(PlayerAccount account, Player? player, DateTime nowUtc)
    {
        if (player is null || account.DiscordUserId is null || account.DiscordLinkRewardClaimedAtUtc is not null)
            return new DiscordLinkRewardResult(false, 0, 0, 0);

        account.DiscordLinkRewardClaimedAtUtc = nowUtc;
        player.Cash += Cash;
        player.Condoms += Condoms;
        player.Beer += Beer;

        return new DiscordLinkRewardResult(true, Cash, Condoms, Beer);
    }
}
