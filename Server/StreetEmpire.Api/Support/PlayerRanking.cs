using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Mapping;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Mapping.ResponseMappers;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Support;

/// <summary>Assigns global ranks to an already ordered and capped page of players.</summary>
internal static class PlayerRanking
{

    /// <summary>
    /// Assigns global ranks to an already ordered and capped page of players. Only the players who
    /// outrank the weakest row on the page are read back, and only as bare standings, so an unfiltered
    /// page costs about as many rows as the page itself.
    /// </summary>
    internal static async Task<List<RankedPlayer>> RankPageAsync(
        List<Player> page,
        GameDbContext db,
        EconomyService economy,
        CancellationToken cancellationToken)
    {
        if (page.Count == 0)
            return [];

        var playerIds = page.Select(x => x.Id).ToList();
        var standingsByPlayer = await db.Players.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .Select(economy.StandingRowExpression())
            .ToDictionaryAsync(x => x.PlayerId, x => new PlayerStanding(x.NetWorth, x.CreatedAtUtc), cancellationToken);

        var standings = page
            .Select(x => standingsByPlayer.GetValueOrDefault(
                x.Id,
                new PlayerStanding(economy.CalculateNetWorth(x), x.CreatedAtUtc)))
            .ToList();
        var weakest = standings
            .OrderBy(x => x.NetWorth)
            .ThenByDescending(x => x.CreatedAtUtc)
            .First();
        var contenders = await db.Players.AsNoTracking()
            .Where(economy.RanksAbove(weakest.NetWorth, weakest.CreatedAtUtc))
            .Select(economy.StandingExpression())
            .ToListAsync(cancellationToken);

        return page
            .Select((x, index) => new RankedPlayer(
                x,
                standings[index].NetWorth,
                economy.CalculatePlunder(x),
                EconomyService.RankOf(standings[index], contenders)))
            .ToList();
    }
}
