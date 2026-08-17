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

/// <summary>Templates for the seeded AI rivals.</summary>
internal static class BotSeeding
{

    internal static Player CreateBotPlayer(BotTemplate template, GameOptions options, DateTime createdAtUtc, int maxStorageLevel, int maxSafeLevel)
    {
        var account = new PlayerAccount
        {
            Username = template.Username,
            PasswordHash = "BOT_ACCOUNT_DISABLED",
            IsBot = true,
            CreatedAtUtc = createdAtUtc
        };

        var player = new Player
        {
            Account = account,
            Name = template.Name,
            City = template.City,
            Cash = options.StartingCash + template.CashBonus,
            BankCash = options.StartingBankCash + template.BankCash,
            Turns = Math.Min(options.MaxTurns, options.StartingTurns + template.TurnBonus),
            Pimps = options.StartingPimps + template.Pimps,
            Hoes = options.StartingHoes + template.Hoes,
            Thugs = options.StartingThugs + template.Thugs,
            HoeCutPercent = template.HoeCutPercent,
            HoeHappiness = template.HoeHappiness,
            ThugHappiness = template.ThugHappiness,
            Condoms = options.StartingCondoms + template.Condoms,
            Beer = options.StartingBeer + template.Beer,
            Weapons = options.StartingWeapons + template.Weapons,
            Weed = template.Weed,
            Coke = template.Coke,
            LastTurnUpdateUtc = createdAtUtc,
            CreatedAtUtc = createdAtUtc
        };
        // Rivals are seeded as established operations, then clamped so they obey the same limits players do.
        player.Hideout = new Hideout
        {
            Player = player,
            StorageLevel = maxStorageLevel,
            SafeLevel = maxSafeLevel,
            CreatedAtUtc = createdAtUtc
        };
        return player;
    }

    internal static IReadOnlyList<BotTemplate> BotTemplates() =>
    [
        new("ai_silk_ledger", "Silk Ledger", "New York", 12_000, 6_000, 40, 2, 24, 8, 180, 120, 10, 100, 35, 35, 92, 88),
        new("ai_brass_knox", "Brass Knox", "Chicago", 9_000, 8_500, 25, 3, 18, 12, 140, 160, 16, 55, 20, 30, 86, 94),
        new("ai_velvet_bishop", "Velvet Bishop", "Miami", 16_000, 2_000, 55, 1, 32, 6, 230, 90, 8, 145, 42, 40, 96, 82),
        new("ai_night_audit", "Night Audit", "Detroit", 7_500, 11_000, 20, 4, 14, 16, 110, 210, 20, 40, 18, 25, 80, 91),
        new("ai_lucky_voss", "Lucky Voss", "Los Angeles", 20_000, 5_000, 70, 2, 28, 10, 220, 150, 13, 180, 60, 35, 89, 87),
        new("ai_ruby_ledger", "Ruby Ledger", "New York", 5_500, 18_000, 15, 5, 20, 18, 160, 250, 24, 35, 12, 30, 84, 95),
        new("ai_metro_saint", "Metro Saint", "Chicago", 11_000, 4_500, 35, 2, 22, 9, 170, 130, 12, 95, 28, 35, 90, 86),
        new("ai_grit_baron", "Grit Baron", "Detroit", 4_000, 22_000, 10, 6, 16, 22, 120, 280, 28, 20, 8, 30, 78, 96),
        new("ai_crown_vale", "Crown Vale", "Miami", 24_000, 1_500, 80, 1, 36, 7, 260, 110, 9, 220, 75, 45, 98, 84),
        new("ai_switch_lane", "Switch Lane", "Los Angeles", 8_000, 7_000, 30, 3, 19, 11, 150, 145, 14, 85, 25, 30, 87, 89),
        new("ai_ace_borough", "Ace Borough", "New York", 15_000, 10_000, 45, 4, 25, 14, 210, 200, 18, 120, 44, 35, 91, 92),
        new("ai_dollar_wren", "Dollar Wren", "Chicago", 6_500, 14_000, 18, 2, 17, 13, 130, 175, 17, 48, 16, 30, 82, 90),
        new("ai_queen_mercer", "Queen Mercer", "Miami", 18_000, 9_000, 60, 3, 30, 12, 240, 190, 16, 165, 58, 40, 95, 90),
        new("ai_brick_falcon", "Brick Falcon", "Detroit", 10_000, 12_000, 28, 5, 21, 20, 170, 260, 26, 70, 30, 30, 83, 97),
        new("ai_halo_vice", "Halo Vice", "Los Angeles", 22_000, 3_000, 75, 2, 34, 9, 255, 135, 12, 205, 70, 45, 97, 86),

        // Three to a town, every town. A city with no rivals is a city with an empty leaderboard and
        // nobody to fight, so adding a town to the map without adding names to it only looks like a
        // choice at sign-up.
        new("ai_desert_lily", "Desert Lily", "Las Vegas", 21_000, 4_000, 65, 2, 31, 8, 235, 125, 11, 190, 64, 40, 94, 85),
        new("ai_chip_calloway", "Chip Calloway", "Las Vegas", 9_500, 13_000, 22, 4, 18, 15, 145, 195, 19, 60, 22, 30, 81, 93),
        new("ai_neon_royce", "Neon Royce", "Las Vegas", 14_000, 6_500, 42, 3, 26, 11, 195, 155, 14, 130, 46, 35, 90, 88),

        new("ai_peach_dandridge", "Peach Dandridge", "Atlanta", 13_000, 7_500, 38, 3, 27, 10, 200, 145, 13, 125, 40, 35, 92, 87),
        new("ai_bank_holloway", "Bank Holloway", "Atlanta", 5_000, 19_000, 14, 5, 19, 19, 155, 265, 25, 32, 11, 30, 79, 95),
        new("ai_stax_pemberton", "Stax Pemberton", "Atlanta", 17_500, 5_500, 52, 2, 29, 9, 225, 140, 12, 155, 52, 40, 93, 86),

        new("ai_gulf_marchetti", "Gulf Marchetti", "Houston", 19_000, 8_000, 58, 3, 30, 13, 230, 185, 16, 175, 56, 40, 94, 89),
        new("ai_derrick_salas", "Derrick Salas", "Houston", 7_000, 16_000, 19, 5, 17, 21, 135, 270, 27, 38, 14, 30, 80, 96),
        new("ai_bayou_kincaid", "Bayou Kincaid", "Houston", 11_500, 9_500, 33, 3, 23, 12, 175, 170, 15, 100, 34, 35, 88, 90)
    ];
}
