using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Administrative actions on other players. Everything here writes an audit entry naming the actor,
/// the target, and the before and after values, because an admin panel without a paper trail is worse
/// than none: you cannot tell a mistake from abuse after the fact.
/// </summary>
public sealed class AdminService(
    GameDbContext db,
    EconomyService economy,
    HideoutService hideouts,
    PimpRoster pimps,
    IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>Resources an admin can move, with the setter and reader for each.</summary>
    private static readonly Dictionary<string, ResourceHandle> Resources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cash"] = new(p => p.Cash, (p, v) => p.Cash = v, long.MaxValue),
        ["bank"] = new(p => p.BankCash, (p, v) => p.BankCash = v, long.MaxValue),
        ["turns"] = new(p => p.Turns, (p, v) => p.Turns = (int)v, int.MaxValue),
        ["pimps"] = new(p => p.Pimps, (p, v) => p.Pimps = (int)v, int.MaxValue),
        ["hoes"] = new(p => p.Hoes, (p, v) => p.Hoes = (int)v, int.MaxValue),
        ["thugs"] = new(p => p.Thugs, (p, v) => p.Thugs = (int)v, int.MaxValue),
        ["condoms"] = new(p => p.Condoms, (p, v) => p.Condoms = (int)v, int.MaxValue),
        ["beer"] = new(p => p.Beer, (p, v) => p.Beer = (int)v, int.MaxValue),
        ["weapons"] = new(p => p.Weapons, (p, v) => p.Weapons = (int)v, int.MaxValue),
        ["weed"] = new(p => p.Weed, (p, v) => p.Weed = (int)v, int.MaxValue),
        ["coke"] = new(p => p.Coke, (p, v) => p.Coke = (int)v, int.MaxValue)
    };

    public static IReadOnlyCollection<string> AdjustableResources => Resources.Keys.ToList();

    public IQueryable<Player> SearchPlayers(string? query)
    {
        var players = db.Players.Include(x => x.Account).AsNoTracking();
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return players;

        // ILIKE keeps admin search case-insensitive; the escape keeps typed wildcards literal.
        var pattern = $"%{trimmed.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        return players.Where(x =>
            EF.Functions.ILike(x.Name, pattern, "\\")
            || EF.Functions.ILike(x.Account.Username, pattern, "\\")
            || EF.Functions.ILike(x.City, pattern, "\\"));
    }

    public Task<Player?> FindPlayerAsync(Guid playerId, CancellationToken cancellationToken)
        => db.Players
            .Include(x => x.Account)
            .Include(x => x.Hideout)
            .Include(x => x.Crew)
            .SingleOrDefaultAsync(x => x.Id == playerId, cancellationToken);

    /// <summary>
    /// Moves a resource by a signed delta so an admin can correct a mistake in either direction, which
    /// the old self-only cheat could not do. Clamped at zero rather than allowed to go negative.
    /// </summary>
    public string AdjustResource(PlayerAccount actor, Player target, string? resource, long delta, string? reason, DateTime nowUtc)
    {
        var key = resource?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Resources.TryGetValue(key, out var handle))
            throw new GameRuleException($"Resource must be one of: {string.Join(", ", AdjustableResources)}.");
        if (delta == 0)
            throw new GameRuleException("Adjustment cannot be zero.");
        if (Math.Abs(delta) > 1_000_000_000)
            throw new GameRuleException("Adjustment is too large.");

        var before = handle.Read(target);
        var after = Math.Clamp(before + delta, 0, handle.Ceiling);
        handle.Write(target, after);

        // Turns still respect the cap, and the pimp roster has to follow its counter.
        if (key == "turns")
            target.Turns = Math.Min(target.Turns, _options.MaxTurns);
        if (key == "pimps")
            pimps.Reconcile(target, nowUtc);

        var summary = $"{key}: {before:N0} -> {handle.Read(target):N0} ({(delta > 0 ? "+" : string.Empty)}{delta:N0})";
        Record(actor, "Adjust", target, summary, reason, nowUtc);
        return summary;
    }

    public string SetMorale(PlayerAccount actor, Player target, double morale, string? reason, DateTime nowUtc)
    {
        if (morale is < 0 or > 100)
            throw new GameRuleException("Morale must be between 0 and 100.");

        var summary = $"morale: hoes {target.HoeHappiness:N0}/thugs {target.ThugHappiness:N0} -> {morale:N0}";
        target.HoeHappiness = morale;
        target.ThugHappiness = morale;
        Record(actor, "SetMorale", target, summary, reason, nowUtc);
        return summary;
    }

    public string SetEnforcement(PlayerAccount actor, Player target, string? action, DateTime? until, string? reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new GameRuleException("Account actions need a reason for the audit trail.");
        if (target.Account.IsAdmin)
            throw new GameRuleException("Admin accounts cannot be banned or suspended. Demote them first.");

        var key = action?.Trim().ToLowerInvariant() ?? string.Empty;
        string summary;
        switch (key)
        {
            case "ban":
                target.Account.IsBanned = true;
                target.Account.SuspendedUntilUtc = null;
                target.Account.EnforcementReason = reason;
                // Ends any live session, which cookie auth would otherwise keep honouring.
                target.Account.SessionsValidAfterUtc = nowUtc;
                summary = "banned indefinitely";
                break;
            case "suspend":
                if (until is not { } expiry || expiry <= nowUtc)
                    throw new GameRuleException("A suspension needs an expiry in the future.");
                target.Account.IsBanned = false;
                target.Account.SuspendedUntilUtc = expiry;
                target.Account.EnforcementReason = reason;
                target.Account.SessionsValidAfterUtc = nowUtc;
                summary = $"suspended until {expiry:u}";
                break;
            case "clear":
                target.Account.IsBanned = false;
                target.Account.SuspendedUntilUtc = null;
                target.Account.EnforcementReason = null;
                summary = "ban and suspension lifted";
                break;
            default:
                throw new GameRuleException("Enforcement must be 'ban', 'suspend', or 'clear'.");
        }

        Record(actor, "Enforcement", target, summary, reason, nowUtc);
        return summary;
    }

    /// <summary>Ends a player's sessions without otherwise penalising them.</summary>
    public string ForceLogout(PlayerAccount actor, Player target, string? reason, DateTime nowUtc)
    {
        target.Account.SessionsValidAfterUtc = nowUtc;
        Record(actor, "ForceLogout", target, "sessions invalidated", reason, nowUtc);
        return "sessions invalidated";
    }

    /// <summary>
    /// Grants or revokes admin. Refuses to remove the last admin, which would leave the game with no
    /// way back in short of direct database access.
    /// </summary>
    public async Task<string> SetAdminAsync(PlayerAccount actor, Player target, bool isAdmin, string? reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new GameRuleException("Changing admin rights needs a reason for the audit trail.");
        if (target.Account.IsBot)
            throw new GameRuleException("AI accounts cannot be made admins.");
        if (target.Account.IsAdmin == isAdmin)
            throw new GameRuleException(isAdmin ? "That account is already an admin." : "That account is not an admin.");

        if (!isAdmin)
        {
            var admins = await db.Accounts.CountAsync(x => x.IsAdmin, cancellationToken);
            if (admins <= 1)
                throw new GameRuleException("You cannot remove the last admin.");
        }

        target.Account.IsAdmin = isAdmin;
        var summary = isAdmin ? "granted admin" : "revoked admin";
        Record(actor, "SetAdmin", target, summary, reason, nowUtc);
        return summary;
    }

    public string Rename(PlayerAccount actor, Player target, string? name, string? reason, DateTime nowUtc)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 3 or > 32)
            throw new GameRuleException("Player name must be 3-32 characters.");

        var summary = $"renamed {target.Name} -> {trimmed}";
        target.Name = trimmed;
        Record(actor, "Rename", target, summary, reason, nowUtc);
        return summary;
    }

    /// <summary>Writes an audit entry. Every mutating method above funnels through here.</summary>
    public void Record(PlayerAccount actor, string action, Player? target, string summary, string? reason, DateTime nowUtc)
        => db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorAccountId = actor.Id,
            ActorUsername = actor.Username,
            Action = action,
            TargetPlayerId = target?.Id,
            TargetName = target?.Name,
            Summary = summary,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAtUtc = nowUtc
        });

    public IQueryable<AdminAuditLog> AuditTrail()
        => db.AdminAuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id);

    public long NetWorthOf(Player player) => economy.CalculateNetWorth(player);
    public HideoutCapacity CapacityOf(Player player) => hideouts.CapacityFor(player.Hideout);

    private sealed record ResourceHandle(Func<Player, long> Read, Action<Player, long> Write, long Ceiling);
}
