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
        player.Coke);

    internal static void AddLog(
        GameDbContext db,
        Player player,
        PlayerSnapshot before,
        string action,
        int turnsSpent,
        string summary)
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
            Summary = summary
        });
    }
}
