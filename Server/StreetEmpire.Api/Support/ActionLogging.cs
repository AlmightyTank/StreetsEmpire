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
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Support;

/// <summary>Before-and-after snapshots that turn a resolved action into a log row.</summary>
internal static class ActionLogging
{

    internal static PlayerSnapshot Snapshot(Player player) => new(
        player.Cash,
        player.BankCash,
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.Condoms,
        player.Beer,
        player.Weapons,
        player.Weed,
        player.Coke,
        player.HoeHappiness,
        player.ThugHappiness);

    /// <summary>
    /// What an action charged, read back off its own breakdown.
    ///
    /// For the actions that decide their own turn cost rather than being handed one - travel prices
    /// the distance, a trip to the bank is free inside its grace window - the resolved cost is only
    /// knowable after the fact, and the log row has to carry the number that was actually taken.
    /// </summary>
    internal static int TurnsSpentIn(ActionResultResponse result)
        => result.Breakdown is not null && result.Breakdown.TryGetValue("turnsSpent", out var spent) && spent is not null
            ? Convert.ToInt32(spent, CultureInfo.InvariantCulture)
            : 0;

    /// <param name="createdAtUtc">
    /// Stamp the row with the caller's clock instead of the moment the object happens to be built.
    /// A row left to default sits a few ticks after the request's own "now", which is enough for a
    /// watermark set to that "now" to consider the row still unseen and replay it on the next read.
    /// </param>
    internal static void AddLog(
        GameDbContext db,
        Player player,
        PlayerSnapshot before,
        string action,
        int turnsSpent,
        string summary,
        DateTime? createdAtUtc = null)
    {
        db.ActionLogs.Add(new GameActionLog
        {
            PlayerId = player.Id,
            Action = action,
            TurnsSpent = turnsSpent,
            CashDelta = player.Cash - before.Cash,
            BankDelta = player.BankCash - before.BankCash,
            PimpsDelta = player.Pimps - before.Pimps,
            HoesDelta = player.Hoes - before.Hoes,
            ThugsDelta = player.Thugs - before.Thugs,
            CondomsDelta = player.Condoms - before.Condoms,
            BeerDelta = player.Beer - before.Beer,
            WeaponsDelta = player.Weapons - before.Weapons,
            WeedDelta = player.Weed - before.Weed,
            CokeDelta = player.Coke - before.Coke,
            HoeMoraleBefore = before.HoeMorale,
            ThugMoraleBefore = before.ThugMorale,
            Summary = summary,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        });
    }
}
