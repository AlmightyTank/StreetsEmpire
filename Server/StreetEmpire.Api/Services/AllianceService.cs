using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Who runs with whom.
///
/// Every rule about membership lives here rather than in the endpoints, because almost all of them are
/// really the same rule seen from different sides - you are in one crew, a crew holds so many, and the
/// founder is a member like anyone else. Written out per endpoint, founding would forget a check that
/// joining remembered.
/// </summary>
public sealed class AllianceService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    EconomyService economy,
    HideoutService? hideouts = null,
    TerritoryService? territories = null,
    DiscordDirectMessages? discordDms = null)
{
    private static readonly TimeSpan NameChangeCooldown = TimeSpan.FromHours(24);
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Whether these two have agreed not to rob each other. The single question every attack path asks,
    /// and the reason a null alliance on either side is emphatically not a match: two unaligned players
    /// are not allies, they are two people with nothing between them.
    /// </summary>
    public static bool AreAllied(Player one, Player other)
        => one.AllianceId is { } crew && other.AllianceId == crew;

    /// <summary>
    /// Whether two players are covered by a truce, in either direction.
    ///
    /// The question every fight asks before it is allowed to start, so it has to answer the same way
    /// round both ways: a pact is one row naming two crews, and which of them happens to be stored as
    /// the requester is an accident of who asked first.
    ///
    /// Only an active pact counts. A pending one is a crew that has been asked, not a crew that has
    /// agreed, and treating the asking as the agreement would let anybody buy an afternoon's immunity
    /// by requesting a pact with whoever is about to hit them.
    /// </summary>
    public async Task<bool> AreAlliedAsync(Player one, Player other, CancellationToken cancellationToken)
    {
        if (AreAllied(one, other))
            return true;
        if (one.AllianceId is not { } mine || other.AllianceId is not { } theirs)
            return false;

        return await db.AlliancePacts.AsNoTracking()
            .AnyAsync(x => x.Status == AlliancePactStatuses.Active
                           && ((x.RequestingAllianceId == mine && x.TargetAllianceId == theirs)
                               || (x.RequestingAllianceId == theirs && x.TargetAllianceId == mine)),
                cancellationToken);
    }

    /// <summary>
    /// Hands something of yours to somebody in your crew.
    ///
    /// The rule underneath every check here is that a transfer moves goods and never makes them. What
    /// leaves the sender is exactly what reaches the receiver, so the sender must genuinely hold it -
    /// thugs standing at home rather than out on a raid or holding ground - and the receiver must have
    /// somewhere to put it, or the overflow would simply vanish.
    ///
    /// Recorded rather than done silently: once stock is in somebody else's pile it is indistinguishable
    /// from their own, and this row is the only account of where a crew's things went.
    /// </summary>
    public async Task<AllianceTransfer> SendResourceAsync(Player sender, Guid receiverId, string? item, int quantity, DateTime nowUtc, CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(sender);
        if (sender.AllianceId is not { } allianceId)
            throw new GameRuleException("You are not running with a crew.");
        if (receiverId == sender.Id)
            throw new GameRuleException("You already have that.");
        if (quantity < 1)
            throw new GameRuleException("Send at least one.");

        var receiver = await db.Players
            .Include(x => x.Hideout)
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == receiverId, cancellationToken)
            ?? throw new GameRuleException("No such player.");
        if (receiver.AllianceId != allianceId)
            throw new GameRuleException($"{receiver.Name} is not in your crew.");

        var key = NormaliseResource(item);
        await MoveResourceAsync(sender, receiver, key, quantity, enforceReceiverRoom: true, cancellationToken);

        var transfer = new AllianceTransfer
        {
            AllianceId = allianceId,
            FromPlayerId = sender.Id,
            FromPlayer = sender,
            ToPlayerId = receiver.Id,
            ToPlayer = receiver,
            Item = key,
            Quantity = quantity,
            CreatedAtUtc = nowUtc
        };
        db.AllianceTransfers.Add(transfer);

        // The receiver is told, for the same reason a market sale tells the seller: it happened to them
        // rather than because of them. Guns and thugs arriving unannounced is the version of this that
        // players notice a week later while wondering where their crew came from.
        await TellAsync(receiver, $"{sender.Name} sent you {quantity:N0} {key}.", nowUtc, cancellationToken);
        return transfer;
    }

    /// <summary>
    /// A crew notice, for the bell. Only ever written to somebody who was not the one acting - a log of
    /// your own doing belongs in your activity, and would make the crew switch on the account page a
    /// switch for being told what you just did.
    ///
    /// Never to a bot. Ten minutes of a test world wrote 129 assist notices and 119 of them went to
    /// accounts that will never open a bell.
    /// </summary>
    private async Task TellAsync(Player recipient, string summary, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (recipient.Account?.IsBot == true) return;
        Tell(recipient.Id, summary, nowUtc);
        if (discordDms is not null && recipient.Account is not null)
            await discordDms.TellGameAlertAsync(
                recipient.Account,
                AlertCategory.Crew,
                "Crew business needs eyes",
                summary,
                nowUtc,
                cancellationToken);
    }

    /// <summary>By id, for the caller that has already left the bots out of its query.</summary>
    private void Tell(Guid recipientId, string summary, DateTime nowUtc)
        => db.ActionLogs.Add(new GameActionLog
        {
            PlayerId = recipientId,
            Action = "CREW",
            Summary = summary,
            CreatedAtUtc = nowUtc,
        });

    /// <summary>
    /// Asks another crew for a truce. Nothing is agreed until they answer.
    ///
    /// One live pact or request per pair, checked in both directions - without it a crew could bury
    /// another under requests, and two crews could end up holding two separate truces with each other
    /// that cancel independently.
    /// </summary>
    public async Task<AlliancePact> RequestPactAsync(Player actor, long targetAllianceId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        EnsurePower(actor, alliance, AlliancePower.Invite);
        if (targetAllianceId == alliance.Id)
            throw new GameRuleException("You are already that crew.");

        var target = await db.Alliances.SingleOrDefaultAsync(x => x.Id == targetAllianceId, cancellationToken)
            ?? throw new GameRuleException("That crew does not exist.");

        if (await AnyLivePactAsync(alliance.Id, target.Id, cancellationToken))
            throw new GameRuleException($"{target.Name} already has a pact or a pact request with you.");

        var pact = new AlliancePact
        {
            RequestingAllianceId = alliance.Id,
            RequestingAlliance = alliance,
            TargetAllianceId = target.Id,
            TargetAlliance = target,
            RequestedById = actor.Id,
            RequestedBy = actor,
            Status = AlliancePactStatuses.Pending,
            CreatedAtUtc = nowUtc
        };
        db.AlliancePacts.Add(pact);
        return pact;
    }

    /// <summary>
    /// Says yes or no to a truce, and only the crew that was asked may.
    ///
    /// Answered once. A pact that could be answered repeatedly would be a switch, and a truce anybody
    /// can flick on the moment a raid launches is not a truce.
    /// </summary>
    public async Task<AlliancePact> AnswerPactAsync(Player actor, long pactId, bool accept, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        EnsurePower(actor, alliance, AlliancePower.Invite);

        var pact = await db.AlliancePacts
            .Include(x => x.RequestingAlliance)
            .Include(x => x.TargetAlliance)
            .SingleOrDefaultAsync(x => x.Id == pactId, cancellationToken)
            ?? throw new GameRuleException("That pact request is gone.");
        if (pact.TargetAllianceId != alliance.Id)
            throw new GameRuleException("That pact request is not for your crew.");
        if (pact.Status != AlliancePactStatuses.Pending)
            throw new GameRuleException("That pact request has already been answered.");

        pact.Status = accept ? AlliancePactStatuses.Active : AlliancePactStatuses.Declined;
        pact.AnsweredById = actor.Id;
        pact.AnsweredBy = actor;
        pact.AnsweredAtUtc = nowUtc;
        return pact;
    }

    /// <summary>
    /// Walks away from a truce, or withdraws the offer of one.
    ///
    /// Either side, which is what keeps it an agreement rather than a trap: a crew that could be held
    /// to a pact it no longer wants would be a crew that cannot defend itself against an ally.
    /// </summary>
    public async Task<AlliancePact> CancelPactAsync(Player actor, long pactId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        EnsurePower(actor, alliance, AlliancePower.Invite);

        var pact = await db.AlliancePacts
            .Include(x => x.RequestingAlliance)
            .Include(x => x.TargetAlliance)
            .SingleOrDefaultAsync(x => x.Id == pactId, cancellationToken)
            ?? throw new GameRuleException("That pact is gone.");
        if (pact.RequestingAllianceId != alliance.Id && pact.TargetAllianceId != alliance.Id)
            throw new GameRuleException("That pact is not yours.");
        if (pact.Status != AlliancePactStatuses.Pending && pact.Status != AlliancePactStatuses.Active)
            throw new GameRuleException("That pact is already closed.");

        pact.Status = AlliancePactStatuses.Canceled;
        pact.AnsweredById = actor.Id;
        pact.AnsweredBy = actor;
        pact.AnsweredAtUtc = nowUtc;
        return pact;
    }

    /// <summary>
    /// Declares war on another crew: a clock, a score and a pot on the table.
    ///
    /// Governed by the treasury power rather than a new one, because that is honestly what declaring
    /// is - somebody spending the crew's money on a fight everybody in it is now part of. The stake
    /// leaves the treasury here, not at the end, so the decision is felt on the day it is made.
    /// </summary>
    public async Task<AllianceWar> DeclareWarAsync(Player actor, long targetAllianceId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        EnsurePower(actor, alliance, AlliancePower.SpendTreasury);
        if (targetAllianceId == alliance.Id)
            throw new GameRuleException("You cannot declare war on yourselves.");

        var target = await db.Alliances.SingleOrDefaultAsync(x => x.Id == targetAllianceId, cancellationToken)
            ?? throw new GameRuleException("That crew does not exist.");

        // A truce and a war are the same question answered two ways, so one has to be ended before the
        // other can start. Cancelling the pact first is a move the other crew can see coming, which is
        // the whole difference between calling a war and simply reneging on a deal.
        if (await AnyLivePactAsync(alliance.Id, target.Id, cancellationToken))
            throw new GameRuleException($"You hold a pact with {target.Name}. End it before you declare on them.");

        var config = _options.Alliances.War;
        var live = await db.AllianceWars
            .Where(x => x.Status == AllianceWarStatuses.Active)
            .Where(x => x.DeclaringAllianceId == alliance.Id || x.TargetAllianceId == alliance.Id
                        || x.DeclaringAllianceId == target.Id || x.TargetAllianceId == target.Id)
            .ToListAsync(cancellationToken);
        // One war at a time, per crew rather than per pair. A crew fighting on two fronts is a crew
        // whose members cannot tell which fights are scoring, and the score is the whole point.
        if (live.Any(x => x.DeclaringAllianceId == alliance.Id || x.TargetAllianceId == alliance.Id))
            throw new GameRuleException("You are already at war. Finish that one first.");
        if (live.Count > 0)
            throw new GameRuleException($"{target.Name} is already at war with somebody else.");

        var cooledOffAfter = nowUtc.AddHours(-Math.Max(0, config.CooldownHours));
        var recent = await db.AllianceWars
            .Where(x => x.Status == AllianceWarStatuses.Settled && x.SettledAtUtc > cooledOffAfter)
            .Where(x => (x.DeclaringAllianceId == alliance.Id && x.TargetAllianceId == target.Id)
                        || (x.DeclaringAllianceId == target.Id && x.TargetAllianceId == alliance.Id))
            .OrderByDescending(x => x.SettledAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null)
            throw new GameRuleException($"You fought {target.Name} too recently. Wait {config.CooldownHours} hours between wars with the same crew.");

        var stake = Math.Max(0, config.Stake);
        if (alliance.Treasury < stake)
            throw new GameRuleException($"Declaring costs the treasury {stake:C0} and you hold {alliance.Treasury:C0}.");

        alliance.Treasury -= stake;
        var war = new AllianceWar
        {
            DeclaringAllianceId = alliance.Id,
            DeclaringAlliance = alliance,
            TargetAllianceId = target.Id,
            TargetAlliance = target,
            DeclaredById = actor.Id,
            DeclaredBy = actor,
            Status = AllianceWarStatuses.Active,
            Stake = stake,
            StartedAtUtc = nowUtc,
            EndsAtUtc = nowUtc.AddHours(Math.Max(1, config.DurationHours))
        };
        db.AllianceWars.Add(war);

        // One public row, on the player who declared it, because crews do not write to the news feed -
        // people do. Everybody in both crews gets told separately, in their own activity.
        db.ActionLogs.Add(new GameActionLog
        {
            PlayerId = actor.Id,
            Action = "WAR",
            Summary = $"{alliance.Name} declared war on {target.Name}, and put {stake:C0} on it.",
            CashDelta = 0,
            CreatedAtUtc = nowUtc
        });
        await TellBothCrewsAsync(
            war,
            $"{alliance.Name} declared war on {target.Name}. {config.DurationHours} hours, and the pot is {stake:C0} plus a cut of the losing treasury.",
            nowUtc,
            cancellationToken);

        return war;
    }

    /// <summary>
    /// Scores a finished fight, when it happened between two crews with a war on.
    ///
    /// Nothing new is counted. A raid won, a raid turned away and a piece of ground taken are outcomes
    /// the combat system already produces; a war is only a reason to go and produce them against one
    /// particular crew, and this is where that reason gets written down.
    /// </summary>
    public async Task<AllianceWar?> ScoreWarAsync(CombatMission mission, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (mission.Attacker.AllianceId is not { } attackerCrew || mission.Defender.AllianceId is not { } defenderCrew)
            return null;
        if (attackerCrew == defenderCrew)
            return null;

        var war = await db.AllianceWars
            .Where(x => x.Status == AllianceWarStatuses.Active && x.EndsAtUtc > nowUtc)
            .Where(x => (x.DeclaringAllianceId == attackerCrew && x.TargetAllianceId == defenderCrew)
                        || (x.DeclaringAllianceId == defenderCrew && x.TargetAllianceId == attackerCrew))
            .FirstOrDefaultAsync(cancellationToken);
        if (war is null)
            return null;

        var config = _options.Alliances.War;
        // A standstill scores for nobody, which is correct: neither side achieved anything, and a war
        // where both crews are paid for stalemates is a war nobody has to try to win.
        var (scorer, points) = mission.Outcome switch
        {
            "Victory" when mission.TerritoryId is not null => (attackerCrew, config.PointsForGroundTaken),
            "Victory" => (attackerCrew, config.PointsForRaidWon),
            "Defeat" => (defenderCrew, config.PointsForDefenceHeld),
            _ => (0L, 0)
        };
        if (points <= 0 || scorer == 0)
            return null;

        if (scorer == war.DeclaringAllianceId)
            war.DeclaringScore += points;
        else
            war.TargetScore += points;
        return war;
    }

    /// <summary>
    /// Settles every war whose clock has run out, and pays the winner.
    ///
    /// Run off whoever happens to be looked at next rather than a timer, like everything else here.
    /// The stake goes back to the crew that put it up when nobody won, because a war that was declared
    /// and never fought did not happen, and a crew should not be able to draw a wage by declaring on
    /// people who have stopped playing.
    /// </summary>
    public async Task<int> SettleDueWarsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var due = await db.AllianceWars
            .Include(x => x.DeclaringAlliance)
            .Include(x => x.TargetAlliance)
            .Where(x => x.Status == AllianceWarStatuses.Active && x.EndsAtUtc <= nowUtc)
            .OrderBy(x => x.EndsAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
            return 0;

        var config = _options.Alliances.War;
        foreach (var war in due)
        {
            war.Status = AllianceWarStatuses.Settled;
            war.SettledAtUtc = nowUtc;

            var declaring = war.DeclaringAlliance;
            var target = war.TargetAlliance;
            var best = Math.Max(war.DeclaringScore, war.TargetScore);
            var level = war.DeclaringScore == war.TargetScore;

            if (level || best < config.MinScoreToWin)
            {
                declaring.Treasury += war.Stake;
                war.Outcome = level && best > 0
                    ? $"{declaring.Name} and {target.Name} fought to {war.DeclaringScore} apiece. The stake went home."
                    : $"Neither {declaring.Name} nor {target.Name} did enough to call it a war. The stake went home.";
                await TellBothCrewsAsync(war, war.Outcome, nowUtc, cancellationToken);
                continue;
            }

            var winner = war.DeclaringScore > war.TargetScore ? declaring : target;
            var loser = winner == declaring ? target : declaring;
            var tribute = (long)Math.Min(
                Math.Max(0, config.MaxTribute),
                Math.Max(0, loser.Treasury) * Math.Clamp(config.TributePercent, 0, 100) / 100.0);

            loser.Treasury -= tribute;
            winner.Treasury += war.Stake + tribute;
            war.WinnerAllianceId = winner.Id;
            war.Tribute = tribute;
            war.Outcome = $"{winner.Name} beat {loser.Name} {Math.Max(war.DeclaringScore, war.TargetScore)}-{Math.Min(war.DeclaringScore, war.TargetScore)} and took {war.Stake + tribute:C0}.";

            db.ActionLogs.Add(new GameActionLog
            {
                PlayerId = war.DeclaredById,
                Action = "WAR",
                Summary = war.Outcome,
                CreatedAtUtc = nowUtc
            });
            await TellBothCrewsAsync(war, war.Outcome, nowUtc, cancellationToken);
        }

        // Left unsaved on purpose: the caller owns the save, the way it does for everything else the
        // clock does. A settle that is not saved is simply settled again next time somebody looks,
        // because the only thing that decides whether a war is over is its own status and its clock.
        return due.Count;
    }

    /// <summary>The war a crew is in right now, or null. Both directions, because either side counts.</summary>
    public async Task<AllianceWar?> ActiveWarForAsync(long allianceId, CancellationToken cancellationToken)
        => await db.AllianceWars
            .Include(x => x.DeclaringAlliance)
            .Include(x => x.TargetAlliance)
            .Include(x => x.DeclaredBy)
            .Where(x => x.Status == AllianceWarStatuses.Active)
            .Where(x => x.DeclaringAllianceId == allianceId || x.TargetAllianceId == allianceId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>What a crew has been through, newest first. This is the record a crew is judged on.</summary>
    public async Task<IReadOnlyList<AllianceWar>> WarHistoryForAsync(long allianceId, int take, CancellationToken cancellationToken)
        => await db.AllianceWars
            .AsNoTracking()
            .Include(x => x.DeclaringAlliance)
            .Include(x => x.TargetAlliance)
            .Include(x => x.DeclaredBy)
            .Where(x => x.Status == AllianceWarStatuses.Settled)
            .Where(x => x.DeclaringAllianceId == allianceId || x.TargetAllianceId == allianceId)
            .OrderByDescending(x => x.SettledAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <summary>Wars won and lost by every crew on the board, in one pass rather than one query each.</summary>
    public async Task<IReadOnlyDictionary<long, (int Won, int Lost)>> WarRecordsAsync(CancellationToken cancellationToken)
    {
        var settled = await db.AllianceWars.AsNoTracking()
            .Where(x => x.Status == AllianceWarStatuses.Settled && x.WinnerAllianceId != null)
            .Select(x => new { x.DeclaringAllianceId, x.TargetAllianceId, x.WinnerAllianceId })
            .ToListAsync(cancellationToken);

        var records = new Dictionary<long, (int Won, int Lost)>();
        foreach (var row in settled)
        {
            var winner = row.WinnerAllianceId!.Value;
            var loser = winner == row.DeclaringAllianceId ? row.TargetAllianceId : row.DeclaringAllianceId;
            records.TryGetValue(winner, out var won);
            records[winner] = (won.Won + 1, won.Lost);
            records.TryGetValue(loser, out var lost);
            records[loser] = (lost.Won, lost.Lost + 1);
        }

        return records;
    }

    /// <summary>
    /// Tells everybody in both crews, because a war is the one thing here that happens to a whole crew
    /// rather than to a player. Bots are skipped: they act on rules, not on being told.
    /// </summary>
    private async Task TellBothCrewsAsync(AllianceWar war, string summary, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var members = await db.Players
            .Where(x => !x.Account.IsBot && (x.AllianceId == war.DeclaringAllianceId || x.AllianceId == war.TargetAllianceId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var member in members)
            Tell(member, summary, nowUtc);
    }

    /// <summary>
    /// Raises a call for help with every crew the defender has a truce with, when a raid launches.
    ///
    /// Automatic rather than something the defender does, because a player being raided is frequently
    /// not at the screen - a call that had to be sent by hand would mostly never be sent.
    ///
    /// Not for territory raids. Ground is contested by whoever holds it, and the thing this exists to
    /// answer is somebody's house being kicked in.
    /// </summary>
    public async Task<IReadOnlyList<AllianceAssistCall>> CreateAssistCallsForAsync(CombatMission mission, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (mission.TerritoryId is not null || mission.Defender.AllianceId is not { } defenderAllianceId)
            return [];

        var allyIds = await db.AlliancePacts.AsNoTracking()
            .Where(x => x.Status == AlliancePactStatuses.Active
                        && (x.RequestingAllianceId == defenderAllianceId || x.TargetAllianceId == defenderAllianceId))
            .Select(x => x.RequestingAllianceId == defenderAllianceId ? x.TargetAllianceId : x.RequestingAllianceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (allyIds.Count == 0)
            return [];

        var calls = new List<AllianceAssistCall>();
        foreach (var allyId in allyIds)
        {
            var call = new AllianceAssistCall
            {
                CombatMission = mission,
                DefenderAllianceId = defenderAllianceId,
                AllyAllianceId = allyId,
                Status = AllianceAssistStatuses.Open,
                CreatedAtUtc = nowUtc
            };
            db.AllianceAssistCalls.Add(call);
            calls.Add(call);
        }

        // And somebody is told. The calls were being created and then waiting to be noticed: a player
        // in an allied crew had to happen to open the alliance board while the fight was still running,
        // which for most of them meant every call expired unanswered. This is the notice the bell shows.
        // Humans only, and the bot filter is done in the query rather than left to Tell, because here it
        // is the difference between fetching a handful of rows and fetching a whole world's worth. Ten
        // minutes of a bot world produced 129 of these notices and 119 went to accounts that will never
        // open a bell - permanent storage, and weight in every alert query afterwards, for nobody.
        var allyMembers = await db.Players
            .Include(x => x.Account)
            .AsNoTracking()
            .Where(x => x.AllianceId != null && allyIds.Contains(x.AllianceId.Value) && !x.Account.IsBot)
            .ToListAsync(cancellationToken);

        foreach (var member in allyMembers)
            await TellAsync(member, $"{mission.Defender.Name} is under attack and your crews have a pact. "
                                    + "Send thugs or guns from the alliance board.", nowUtc, cancellationToken);

        return calls;
    }

    /// <summary>
    /// Sends thugs and guns to a crew mate's ally who is being raided.
    ///
    /// The force genuinely moves rather than being counted twice: the fight reads the defender's own
    /// numbers and knows nothing about where they came from, so help that stayed with the ally would
    /// not be help at all. What that costs the ally is real, and taking it back afterwards is a
    /// separate act - see RecallAssistAsync.
    ///
    /// Only while the fight is travelling or being fought. Arriving after the shooting is not help, and
    /// allowing it would make a finished mission a quiet channel for moving thugs about.
    /// </summary>
    public async Task<AllianceAssistCall> AnswerAssistCallAsync(Player actor, long assistCallId, int thugs, Armoury weapons, DateTime nowUtc, CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(actor);
        if (actor.AllianceId is null)
            throw new GameRuleException("You are not running with a crew.");
        if (thugs < 0 || weapons.Pistols < 0 || weapons.Shotguns < 0 || weapons.Smgs < 0 || weapons.Rifles < 0)
            throw new GameRuleException("Send zero or more of each resource.");
        if (thugs == 0 && weapons.Total == 0)
            throw new GameRuleException("Send thugs, guns, or both.");

        var call = await db.AllianceAssistCalls
            .Include(x => x.AllyAlliance)
            .Include(x => x.DefenderAlliance)
            .Include(x => x.CombatMission).ThenInclude(x => x.Attacker)
            .Include(x => x.CombatMission).ThenInclude(x => x.Defender).ThenInclude(x => x.Hideout)
            .SingleOrDefaultAsync(x => x.Id == assistCallId, cancellationToken)
            ?? throw new GameRuleException("That call is gone.");

        if (actor.AllianceId != call.AllyAllianceId)
            throw new GameRuleException("That call is for another crew.");
        if (call.Status != AllianceAssistStatuses.Open)
            throw new GameRuleException("That call has already been answered.");
        if (call.CombatMission.Status != "Traveling" && call.CombatMission.Status != "Fighting")
            throw new GameRuleException("That fight is no longer taking help.");

        var defender = call.CombatMission.Defender;
        await MoveAssistResourcesAsync(actor, defender, thugs, weapons, cancellationToken);

        call.ThugsSent = thugs;
        call.PistolsSent = weapons.Pistols;
        call.ShotgunsSent = weapons.Shotguns;
        call.SmgsSent = weapons.Smgs;
        call.RiflesSent = weapons.Rifles;
        call.RespondedById = actor.Id;
        call.RespondedBy = actor;
        call.RespondedAtUtc = nowUtc;
        call.Status = AllianceAssistStatuses.Answered;
        return call;
    }

    public async Task<Alliance> FoundAsync(Player founder, string? name, string? motto, DateTime nowUtc, CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(founder);
        var config = _options.Alliances;

        if (founder.AllianceId is not null)
            throw new GameRuleException("You are already running with a crew. Leave it before starting your own.");

        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 3 or > 32)
            throw new GameRuleException("A crew name runs from 3 to 32 characters.");
        if (await db.Alliances.AnyAsync(x => x.Name.ToLower() == trimmed.ToLower(), cancellationToken))
            throw new GameRuleException($"There is already a crew called {trimmed}.");
        if (founder.Cash + founder.BankCash < config.FoundingCost)
            throw new GameRuleException($"Founding a crew costs {config.FoundingCost:C0} across your cash and bank.");

        ChargeCapital(founder, config.FoundingCost);

        var alliance = new Alliance
        {
            Name = trimmed,
            Motto = string.IsNullOrWhiteSpace(motto) ? null : motto.Trim(),
            FounderId = founder.Id,
            DuesPercent = Math.Clamp(config.DefaultDuesPercent, 0, config.MaxDuesPercent),
            CreatedAtUtc = nowUtc
        };
        db.Alliances.Add(alliance);

        founder.Alliance = alliance;
        founder.AllianceJoinedAtUtc = nowUtc;
        founder.AllianceRank = AllianceRank.Boss;
        return alliance;
    }

    public async Task<Alliance> JoinAsync(Player player, long allianceId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(player);

        if (player.AllianceId is not null)
            throw new GameRuleException("You are already running with a crew.");

        var alliance = await db.Alliances
            .Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == allianceId, cancellationToken)
            ?? throw new GameRuleException("That crew does not exist.");

        if (alliance.Door != AllianceDoor.Open)
            throw new GameRuleException(alliance.Door == AllianceDoor.Application
                ? $"{alliance.Name} takes people by application. Ask them."
                : $"{alliance.Name} only takes people they have asked for.");
        if (alliance.Members.Count >= MaxMembers)
            throw new GameRuleException($"{alliance.Name} is full at {MaxMembers:N0} members.");

        Admit(player, alliance, nowUtc);
        return alliance;
    }

    /// <summary>
    /// Puts somebody in, at the bottom.
    ///
    /// Every road into a crew ends here - walking through an open door, accepting an invitation, having
    /// an application accepted - so none of them can accidentally hand out a rank, and any rule about
    /// arriving only has to be written once.
    /// </summary>
    private static void Admit(Player player, Alliance alliance, DateTime nowUtc)
    {
        player.AllianceId = alliance.Id;
        player.AllianceJoinedAtUtc = nowUtc;
        player.AllianceRank = AllianceRank.Soldier;
    }

    /// <summary>
    /// Walks out.
    ///
    /// Any defenders standing in this player's house go back to the pool rather than leaving with them.
    /// They were never theirs - they were lent - and a member who could walk off with the crew's thugs
    /// would make leaving a way of robbing the people you had agreed not to rob.
    /// </summary>
    public async Task<Alliance> LeaveAsync(Player player, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(player, cancellationToken);

        if (player.AllianceRank == AllianceRank.Boss && alliance.Members.Count > 1)
            throw new GameRuleException("You run this crew. Hand it to somebody else before you go.");

        ReturnDefenders(player, alliance);
        player.AllianceId = null;
        player.AllianceJoinedAtUtc = null;
        player.AllianceRank = AllianceRank.Soldier;

        // The last one out takes the lights. A crew with nobody in it is a name holding a treasury that
        // nobody can ever reach, so it goes rather than lingering on the board forever.
        if (alliance.Members.Count <= 1)
            db.Alliances.Remove(alliance);

        return alliance;
    }

    public async Task<Alliance> ExpelAsync(Player actor, Guid memberId, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        EnsurePower(actor, alliance, AlliancePower.Expel);

        if (memberId == actor.Id)
            throw new GameRuleException("You cannot throw yourself out. Leave instead.");

        var member = alliance.Members.SingleOrDefault(x => x.Id == memberId)
            ?? throw new GameRuleException("They are not in your crew.");

        // Strictly above, never equal. Two Underbosses able to throw each other out is not a chain of
        // command, it is a fight the crew loses either way.
        if (!AllianceRanks.Outranks(actor.AllianceRank, member.AllianceRank))
            throw new GameRuleException($"{member.Name} is {AllianceRanks.Label(member.AllianceRank)}. You can only throw out somebody below you.");

        ReturnDefenders(member, alliance);
        member.AllianceId = null;
        member.AllianceJoinedAtUtc = null;
        member.AllianceRank = AllianceRank.Soldier;
        return alliance;
    }

    /// <summary>
    /// Moves somebody up or down.
    ///
    /// The boss alone, and never to the top: handing the crew on is its own move precisely because it
    /// is the one that gives yours away, and a promotion that could reach Boss would let a crew acquire
    /// two of them by accident.
    /// </summary>
    public async Task<(Alliance Alliance, Player Member, AllianceRank Rank)> SetRankAsync(Player actor, Guid memberId, string? rank, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        if (actor.AllianceRank != AllianceRank.Boss)
            throw new GameRuleException("Only the boss decides who stands where.");

        var member = alliance.Members.SingleOrDefault(x => x.Id == memberId)
            ?? throw new GameRuleException("They are not in your crew.");
        if (member.Id == actor.Id)
            throw new GameRuleException("Hand the crew over if you want to stop running it.");

        var wanted = AllianceRanks.Parse(rank);
        if (wanted >= AllianceRank.Boss)
            throw new GameRuleException("There is one boss. Hand the crew over instead.");

        member.AllianceRank = wanted;
        return (alliance, member, wanted);
    }

    /// <summary>
    /// Hands the crew to somebody else, and steps down to the rank below.
    ///
    /// One move rather than a promotion followed by a demotion, because the two halves must not be able
    /// to happen separately: a crew with two bosses and a crew with none are both states nothing else
    /// knows how to read, and either would be reachable if this were two calls.
    /// </summary>
    public async Task<(Alliance Alliance, Player Successor)> HandOverAsync(Player actor, Guid memberId, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(actor, cancellationToken);
        if (actor.AllianceRank != AllianceRank.Boss)
            throw new GameRuleException("You are not running this crew.");

        var successor = alliance.Members.SingleOrDefault(x => x.Id == memberId)
            ?? throw new GameRuleException("They are not in your crew.");
        if (successor.Id == actor.Id)
            throw new GameRuleException("You already run it.");

        successor.AllianceRank = AllianceRank.Boss;
        actor.AllianceRank = AllianceRank.Underboss;
        alliance.FounderId = successor.Id;
        return (alliance, successor);
    }

    /// <summary>
    /// The boss's authority: the rate, the door, the sign on it, and where every other line is drawn.
    ///
    /// The thresholds live here rather than beside the powers they gate because they are one decision -
    /// how much of this crew do I run personally - and a boss changing their mind about that should not
    /// have to make it five times in five places.
    /// </summary>
    public async Task<Alliance> UpdateAsync(Player founder, int? duesPercent, string? door, string? motto, IReadOnlyDictionary<string, string>? powers, string? name, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(founder, cancellationToken);
        if (founder.AllianceRank != AllianceRank.Boss)
            throw new GameRuleException("Only the boss decides that.");

        if (name is not null)
        {
            var trimmed = NormalizeName(name);
            if (trimmed.Length is < 3 or > 32)
                throw new GameRuleException("A crew name runs from 3 to 32 characters.");

            if (!string.Equals(trimmed, alliance.Name, StringComparison.Ordinal))
            {
                var now = DateTime.UtcNow;
                if (NextNameChangeAt(alliance.NameChangedAtUtc) is { } readyAt && readyAt > now)
                    throw new GameRuleException($"You can change the crew name again in {FormatWait(readyAt - now)}.");
                if (await db.Alliances.AnyAsync(x => x.Id != alliance.Id && x.Name.ToLower() == trimmed.ToLower(), cancellationToken))
                    throw new GameRuleException("Crew name is already taken.");

                alliance.Name = trimmed;
                alliance.NameChangedAtUtc = now;
            }
        }

        if (duesPercent is { } dues)
        {
            if (dues < 0 || dues > _options.Alliances.MaxDuesPercent)
                throw new GameRuleException($"Dues run from 0 to {_options.Alliances.MaxDuesPercent}%.");
            alliance.DuesPercent = dues;
        }

        if (!string.IsNullOrWhiteSpace(door))
            alliance.Door = AllianceDoors.Parse(door);

        if (motto is not null)
            alliance.Motto = string.IsNullOrWhiteSpace(motto) ? null : motto.Trim()[..Math.Min(motto.Trim().Length, 140)];

        if (powers is not null)
            foreach (var (powerName, rank) in powers)
                if (Enum.TryParse<AlliancePower>(powerName, ignoreCase: true, out var power))
                    alliance.SetMinRankFor(power, AllianceRanks.Parse(rank));

        return alliance;
    }

    private static string NormalizeName(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static DateTime? NextNameChangeAt(DateTime? changedAtUtc)
        => changedAtUtc is { } changedAt ? changedAt.Add(NameChangeCooldown) : null;

    private static int SecondsUntil(DateTime? readyAtUtc, DateTime nowUtc)
        => readyAtUtc is { } readyAt && readyAt > nowUtc
            ? Math.Max(0, (int)Math.Ceiling((readyAt - nowUtc).TotalSeconds))
            : 0;

    private static string FormatWait(TimeSpan wait)
    {
        var totalMinutes = Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes));
        if (totalMinutes >= 60)
            return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
        return $"{totalMinutes}m";
    }

    /// <summary>Whether this member may do a thing here. The one question every gated action asks.</summary>
    public static bool Can(Player member, Alliance alliance, AlliancePower power)
        => member.AllianceId == alliance.Id && member.AllianceRank >= alliance.MinRankFor(power);

    private static void EnsurePower(Player member, Alliance alliance, AlliancePower power)
    {
        if (Can(member, alliance, power)) return;
        var needed = AllianceRanks.Label(alliance.MinRankFor(power));
        throw new GameRuleException($"That is for {needed} and above. You are {AllianceRanks.Label(member.AllianceRank)}.");
    }

    /// <summary>
    /// The board: every crew, worth the sum of what its members are worth.
    ///
    /// Summed in the database off the same net worth expression the leaderboard ranks by, so a crew's
    /// standing and its members' standings can never tell two different stories.
    /// </summary>
    public async Task<IReadOnlyList<AllianceSummaryResponse>> BoardAsync(Player viewer, CancellationToken cancellationToken)
    {
        // Each aligned player's net worth is worked out by the database, off the same expression the
        // individual leaderboard ranks by, and only the totalling happens here - over the aligned
        // players alone, which is dozens of rows rather than the whole table.
        var standings = await db.Players.AsNoTracking()
            .Where(x => x.AllianceId != null)
            .Select(economy.AllianceStandingExpression())
            .ToListAsync(cancellationToken);
        var totals = standings
            .GroupBy(x => x.AllianceId!.Value)
            .ToDictionary(g => g.Key, g => new { NetWorth = g.Sum(x => x.NetWorth), Members = g.Count() });
        var controlled = territories is null
            ? new Dictionary<long, IReadOnlyList<AllianceCityControl>>()
            : await territories.ControlledCitiesByAllianceAsync(cancellationToken);

        var crews = await db.Alliances.AsNoTracking().ToListAsync(cancellationToken);
        // A crew's record and who it is currently fighting, read once for the whole board rather than
        // once per row. Both belong on the board rather than only on a crew's own page: a record you
        // cannot see from outside is not a reputation, and a crew already in a war is one nobody else
        // may declare on, which the button has to know before it is pressed.
        var records = await WarRecordsAsync(cancellationToken);
        var fighting = await db.AllianceWars.AsNoTracking()
            .Include(x => x.DeclaringAlliance)
            .Include(x => x.TargetAlliance)
            .Where(x => x.Status == AllianceWarStatuses.Active)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return crews
            .Select(x =>
            {
                var tally = totals.GetValueOrDefault(x.Id);
                var cityControl = controlled.GetValueOrDefault(x.Id) ?? [];
                var nameChangeReadyAt = NextNameChangeAt(x.NameChangedAtUtc);
                var cityControlResponses = cityControl
                    .Select(city => new AllianceCityControlResponse(city.City, city.Territories, city.BonusThugs))
                    .ToList();
                return new AllianceSummaryResponse(
                    x.Id,
                    x.Name,
                    x.Motto,
                    tally?.Members ?? 0,
                    MaxMembers,
                    tally?.NetWorth ?? 0,
                    x.DuesPercent,
                    x.OffensiveThugs,
                    x.DefensiveThugs,
                    x.Door.ToString(),
                    AllianceDoors.Label(x.Door),
                    AllianceDoors.Describe(x.Door),
                    x.Id == viewer.AllianceId,
                    x.FounderId == viewer.Id,
                    cityControl.Sum(city => city.BonusThugs),
                    cityControlResponses,
                    records.GetValueOrDefault(x.Id).Won,
                    records.GetValueOrDefault(x.Id).Lost,
                    fighting.FirstOrDefault(war => war.DeclaringAllianceId == x.Id || war.TargetAllianceId == x.Id) is { } live
                        ? (live.DeclaringAllianceId == x.Id ? live.TargetAlliance.Name : live.DeclaringAlliance.Name)
                        : null,
                    NameChangeReadyAtUtc: nameChangeReadyAt,
                    NameChangeReadySeconds: SecondsUntil(nameChangeReadyAt, now));
            })
            .OrderByDescending(x => x.NetWorth)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select((x, index) => x with { Rank = index + 1 })
            .ToList();
    }

    /// <summary>
    /// Asks somebody to join, or asks to join somebody.
    ///
    /// Both directions land in one place because they are the same row read from opposite ends, and the
    /// rules that matter are shared: you can only have one crew, a crew only holds so many, and the same
    /// pair of names cannot have two requests outstanding in the same direction.
    ///
    /// An open door does not remove the need for either. Applications are how a closed crew is reachable
    /// at all, and an invitation to an open crew is still worth sending - it is how somebody hears that
    /// there is a place for them rather than having to go looking at a board.
    /// </summary>
    public async Task<AllianceRequest> RequestAsync(Player actor, Guid? subjectId, long? allianceId, AllianceRequestKind kind, string? note, DateTime nowUtc, CancellationToken cancellationToken)
    {
        Alliance alliance;
        Player subject;

        if (kind == AllianceRequestKind.Invitation)
        {
            alliance = await LoadForAsync(actor, cancellationToken);
            EnsurePower(actor, alliance, AlliancePower.Invite);

            var id = subjectId ?? throw new GameRuleException("Say who you are asking.");
            subject = await db.Players
                .Include(x => x.Account)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new GameRuleException("No such player.");
            if (subject.Id == actor.Id)
                throw new GameRuleException("You are already in it.");
        }
        else
        {
            if (actor.AllianceId is not null)
                throw new GameRuleException("You are already running with a crew. Leave it first.");

            var id = allianceId ?? throw new GameRuleException("Say which crew.");
            alliance = await db.Alliances
                .Include(x => x.Members)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new GameRuleException("That crew does not exist.");

            // The door decides what an outsider may do on their own. Asking a crew that would have let
            // you walk in is a formality nobody should have to perform, and asking one that only takes
            // people it has chosen is a message it has said it does not want.
            if (alliance.Door == AllianceDoor.Open)
                throw new GameRuleException($"{alliance.Name} is open. Walk in.");
            if (alliance.Door == AllianceDoor.InviteOnly)
                throw new GameRuleException($"{alliance.Name} only takes people they have asked for.");

            subject = actor;
        }

        if (subject.AllianceId == alliance.Id)
            throw new GameRuleException($"{subject.Name} already runs with {alliance.Name}.");
        if (subject.AllianceId is not null)
            throw new GameRuleException($"{subject.Name} already runs with somebody.");

        var members = alliance.Members.Count > 0
            ? alliance.Members.Count
            : await db.Players.CountAsync(x => x.AllianceId == alliance.Id, cancellationToken);
        if (members >= MaxMembers)
            throw new GameRuleException($"{alliance.Name} is full at {MaxMembers:N0} members.");

        if (await db.AllianceRequests.AnyAsync(x => x.AllianceId == alliance.Id && x.PlayerId == subject.Id && x.Kind == kind, cancellationToken))
            throw new GameRuleException(kind == AllianceRequestKind.Invitation
                ? $"{subject.Name} has already been asked."
                : $"You have already asked {alliance.Name}.");

        var request = new AllianceRequest
        {
            AllianceId = alliance.Id,
            Alliance = alliance,
            PlayerId = subject.Id,
            Player = subject,
            Kind = kind,
            SentById = kind == AllianceRequestKind.Invitation ? actor.Id : null,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 140)],
            CreatedAtUtc = nowUtc
        };
        db.AllianceRequests.Add(request);

        // Only an invitation. An application is the player walking up to the crew and asking, so telling
        // them they did it would be telling them what they just typed - the crew's leaders are the ones
        // who did not know, and their side of this is the alliance board.
        if (kind == AllianceRequestKind.Invitation)
            await TellAsync(subject, $"{alliance.Name} invited you to join. Answer it on the alliance board.", nowUtc, cancellationToken);

        return request;
    }

    /// <summary>
    /// Answers a request, from whichever side is entitled to.
    ///
    /// An invitation is the player's to answer and an application is the crew's, so the check is which
    /// kind this is rather than who is calling - and getting that backwards is exactly the bug worth
    /// making impossible, since either mistake lets somebody put themselves in a crew.
    /// </summary>
    public async Task<(AllianceRequest Request, bool Joined)> AnswerAsync(Player actor, long requestId, bool accept, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var request = await db.AllianceRequests
            .Include(x => x.Alliance).ThenInclude(x => x.Members)
            .Include(x => x.Player)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new GameRuleException("That request is gone.");

        if (request.Kind == AllianceRequestKind.Invitation)
        {
            if (request.PlayerId != actor.Id)
                throw new GameRuleException("That invitation is not yours to answer.");
        }
        else
        {
            if (actor.AllianceId != request.AllianceId)
                throw new GameRuleException("That application is not to your crew.");
            EnsurePower(actor, request.Alliance, AlliancePower.Invite);
        }

        // Gone either way. A refused request that lingered would be a standing argument, and an accepted
        // one has done its job.
        db.AllianceRequests.Remove(request);

        if (!accept)
            return (request, false);

        // Re-checked at the moment of acceptance rather than trusted from when it was sent. Weeks can
        // pass between the two, and in that time the crew can fill up or the player can join somebody.
        var joiner = request.Player;
        if (joiner.AllianceId is not null)
            throw new GameRuleException($"{joiner.Name} has joined somebody else since.");
        if (request.Alliance.Members.Count >= MaxMembers)
            throw new GameRuleException($"{request.Alliance.Name} is full at {MaxMembers:N0} members.");

        Admit(joiner, request.Alliance, nowUtc);

        // Anything else outstanding for this player is moot now, in both directions.
        var stale = await db.AllianceRequests.Where(x => x.PlayerId == joiner.Id).ToListAsync(cancellationToken);
        db.AllianceRequests.RemoveRange(stale);

        return (request, true);
    }

    /// <summary>Takes back something you sent, or that was sent to you.</summary>
    public async Task<AllianceRequest> WithdrawAsync(Player actor, long requestId, CancellationToken cancellationToken)
    {
        var request = await db.AllianceRequests
            .Include(x => x.Alliance)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new GameRuleException("That request is gone.");

        var mine = request.Kind == AllianceRequestKind.Application
            ? request.PlayerId == actor.Id
            : actor.AllianceId == request.AllianceId && Can(actor, request.Alliance, AlliancePower.Invite);
        if (!mine)
            throw new GameRuleException("That is not yours to take back.");

        db.AllianceRequests.Remove(request);
        return request;
    }

    public int MaxMembers => Math.Max(2, _options.Alliances.MaxMembers);

    private async Task<bool> AnyLivePactAsync(long one, long two, CancellationToken cancellationToken)
        => await db.AlliancePacts.AnyAsync(x =>
            x.Status != AlliancePactStatuses.Canceled
            && x.Status != AlliancePactStatuses.Declined
            && ((x.RequestingAllianceId == one && x.TargetAllianceId == two)
                || (x.RequestingAllianceId == two && x.TargetAllianceId == one)),
            cancellationToken);

    private static string NormaliseResource(string? item)
    {
        var key = TradeGoods.Normalise(item);
        if (key is "cash" or "thugs")
            return key;
        if (TradeGoods.IsTradeable(key))
            return key;
        throw new GameRuleException($"You can send cash, thugs, or {string.Join(", ", TradeGoods.Keys)}.");
    }

    private async Task MoveResourceAsync(Player sender, Player receiver, string key, int quantity, bool enforceReceiverRoom, CancellationToken cancellationToken)
    {
        if (key == "cash")
        {
            if (sender.Cash < quantity)
                throw new GameRuleException($"You have {sender.Cash:C0} on hand.");
            sender.Cash -= quantity;
            receiver.Cash += quantity;
            return;
        }

        if (key == "thugs")
        {
            var free = await FreeThugsAsync(sender, cancellationToken);
            if (free < quantity)
                throw new GameRuleException($"You have {free:N0} thug(s) standing free.");
            if (enforceReceiverRoom && hideouts?.CrewRoom(receiver, "thugs") < quantity)
                throw new GameRuleException($"{receiver.Name} does not have room for that many thugs.");
            sender.Thugs -= quantity;
            receiver.Thugs += quantity;
            return;
        }

        var held = TradeGoods.Held(sender, key);
        if (held < quantity)
            throw new GameRuleException($"You only have {held:N0} {TradeGoods.Label(key).ToLowerInvariant()}.");
        if (enforceReceiverRoom && hideouts is not null)
        {
            var room = TradeGoods.Room(receiver, hideouts.CapacityFor(receiver.Hideout), key);
            if (room < quantity)
                throw new GameRuleException($"{receiver.Name} has room for {room:N0} more {TradeGoods.Label(key).ToLowerInvariant()}.");
        }

        TradeGoods.Add(sender, key, -quantity);
        TradeGoods.Add(receiver, key, quantity);
    }

    /// <summary>
    /// Takes back what is left of the help an ally sent, once the fight it was sent to is over.
    ///
    /// Deliberately a thing somebody does rather than something that happens. The alliance pool already
    /// sends its borrowed thugs home by itself when a mission ends, and this could have followed it -
    /// but pool thugs are the crew's and these are one player's, and a crew that wants to leave them
    /// where they are as a gift should be able to. So the ally asks, and gets back whatever is still
    /// standing.
    ///
    /// Never more than was sent, and never more than the defender has free right now. Both caps matter
    /// and for different reasons: the first stops a recall being a way to strip a crew mate of thugs
    /// they always had, and the second is the honest half - some of what was sent will have died in the
    /// fight, and what died is not owed back by anybody.
    /// </summary>
    public async Task<AllianceAssistCall> RecallAssistAsync(Player actor, long assistCallId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(actor);
        if (actor.AllianceId is null)
            throw new GameRuleException("You are not running with a crew.");

        var call = await db.AllianceAssistCalls
            .Include(x => x.CombatMission).ThenInclude(x => x.Defender).ThenInclude(x => x.Hideout)
            .SingleOrDefaultAsync(x => x.Id == assistCallId, cancellationToken)
            ?? throw new GameRuleException("That call is gone.");

        if (actor.AllianceId != call.AllyAllianceId)
            throw new GameRuleException("That call is for another crew.");
        // Whoever sent it is who gets it back. A crew mate cannot collect on somebody else's loan.
        if (call.RespondedById != actor.Id)
            throw new GameRuleException("That help was not yours to send, so it is not yours to take back.");
        if (call.Status != AllianceAssistStatuses.Answered)
            throw new GameRuleException("There is nothing of yours in that fight.");
        // The whole point of sending help is that it is there for the fight. Pulling it out mid-raid
        // would make an assist a gesture somebody can withdraw the moment it costs them.
        if (call.CombatMission.Status != "Complete")
            throw new GameRuleException("That fight is still going. Ask again when it is over.");

        var defender = call.CombatMission.Defender;
        var freeThugs = await FreeThugsAsync(defender, cancellationToken);
        var freeRack = defender.Armoury - await CarriedWeaponsAsync(defender.Id, cancellationToken);

        var thugs = Math.Max(0, Math.Min(call.ThugsSent, freeThugs));
        var guns = new Armoury
        {
            Pistols = Math.Max(0, Math.Min(call.PistolsSent, freeRack.Pistols)),
            Shotguns = Math.Max(0, Math.Min(call.ShotgunsSent, freeRack.Shotguns)),
            Smgs = Math.Max(0, Math.Min(call.SmgsSent, freeRack.Smgs)),
            Rifles = Math.Max(0, Math.Min(call.RiflesSent, freeRack.Rifles)),
        };

        defender.Thugs -= thugs;
        actor.Thugs += thugs;
        defender.Armoury -= guns;
        actor.Armoury += guns;

        call.ThugsReturned = thugs;
        call.PistolsReturned = guns.Pistols;
        call.ShotgunsReturned = guns.Shotguns;
        call.SmgsReturned = guns.Smgs;
        call.RiflesReturned = guns.Rifles;
        call.RecalledAtUtc = nowUtc;
        // Closed either way. A recall that got nothing back is still a recall - the answer is that
        // there was nothing left, and asking again would only ask the same question.
        call.Status = AllianceAssistStatuses.Closed;
        return call;
    }

    /// <summary>
    /// Shuts every open call raised for a fight that has just finished.
    ///
    /// Nothing was doing this, so an unanswered call stayed open for good: the alliance page kept
    /// offering to send help to raids that ended days earlier, and pressing the button answered that
    /// the fight was no longer taking any. Answered calls are left alone, because the ally still has a
    /// claim on what they sent and closing it here would quietly cancel that.
    /// </summary>
    public async Task<int> CloseOpenAssistCallsAsync(long missionId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var open = await db.AllianceAssistCalls
            .Where(x => x.CombatMissionId == missionId && x.Status == AllianceAssistStatuses.Open)
            .ToListAsync(cancellationToken);
        foreach (var call in open)
        {
            call.Status = AllianceAssistStatuses.Closed;
            call.RecalledAtUtc = nowUtc;
        }
        return open.Count;
    }

    private async Task MoveAssistResourcesAsync(Player sender, Player receiver, int thugs, Armoury weapons, CancellationToken cancellationToken)
    {
        if (thugs > 0)
        {
            var freeThugs = await FreeThugsAsync(sender, cancellationToken);
            if (freeThugs < thugs)
                throw new GameRuleException($"You have {freeThugs:N0} thug(s) standing free.");
            sender.Thugs -= thugs;
            receiver.Thugs += thugs;
        }

        if (weapons.Total <= 0)
            return;

        var freeRack = sender.Armoury - await CarriedWeaponsAsync(sender.Id, cancellationToken);
        foreach (var tier in WeaponTiers.All)
        {
            var wanted = weapons.Of(tier);
            if (wanted <= 0) continue;
            if (freeRack.Of(tier) < wanted)
                throw new GameRuleException($"You have {freeRack.Of(tier):N0} {WeaponTiers.Label(tier).ToLowerInvariant()} off the rack.");
        }

        sender.Armoury -= weapons;
        receiver.Armoury += weapons;
    }

    /// <summary>
    /// The thugs a player could actually hand over: what they own, less what is already spoken for.
    ///
    /// Owning a thug and having one standing in front of you are different things. Men out on a raid
    /// or garrisoned on ground still count as the player's, and counting them here would let the same
    /// thug be sent to a crew mate and be holding a territory at the same time.
    /// </summary>
    private async Task<int> FreeThugsAsync(Player player, CancellationToken cancellationToken)
    {
        var onMissions = await db.CombatMissions.AsNoTracking()
            .Where(x => x.AttackerId == player.Id && x.Status != "Complete")
            .SumAsync(x => (int?)x.RemainingAttackers, cancellationToken) ?? 0;
        var garrisoned = await db.Territories.AsNoTracking()
            .Where(x => x.HolderId == player.Id)
            .SumAsync(x => (int?)x.GarrisonThugs, cancellationToken) ?? 0;
        return Math.Max(0, player.Thugs - onMissions - garrisoned);
    }

    private async Task<Armoury> CarriedWeaponsAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var carried = await db.CombatMissions.AsNoTracking()
            .Where(x => x.AttackerId == playerId && x.Status != "Complete")
            .Select(x => new Armoury(x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles))
            .ToListAsync(cancellationToken);
        return carried.Aggregate(Armoury.Empty, (total, rack) => total + rack);
    }

    /// <summary>
    /// How many borrowed thugs this member may field alongside a crew of their own this size.
    ///
    /// The one rule keeping the pool from breaking the fight: alliance thugs ignore the hideout's thug
    /// cap, which is what every balance number is measured against. Tied to the member's own crew, the
    /// pool amplifies instead of substituting - your tier still decides your ceiling and the crew only
    /// doubles it.
    /// </summary>
    public int BorrowLimit(int ownThugs)
        => Math.Max(0, (int)Math.Floor(Math.Max(0, ownThugs) * Math.Max(0, _options.Alliances.MaxBorrowedPerOwnThug)));

    /// <summary>
    /// Buys thugs into the pool out of the treasury.
    ///
    /// The founder's decision because it is the crew's money and somebody has to answer for it. Everyone
    /// pays in; one person decides what it turns into, which is the same arrangement the dues rate is.
    /// </summary>
    public async Task<(Alliance Alliance, int Bought, long Cost)> BuyThugsAsync(Player founder, string? kind, int quantity, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(founder, cancellationToken);
        return (alliance, quantity, BuyThugs(founder, alliance, kind, quantity));
    }

    /// <summary>
    /// The rule itself, over a crew already in hand.
    ///
    /// Split from the loading for the same reason the strike service takes the defending crew rather
    /// than querying for it: every decision here is about two objects and a number, and a method that
    /// also fetches them cannot be tested without a database standing behind it.
    /// </summary>
    public long BuyThugs(Player founder, Alliance alliance, string? kind, int quantity)
    {
        EnsurePower(founder, alliance, AlliancePower.SpendTreasury);

        if (quantity is < 1 or > 10_000)
            throw new GameRuleException("Buy between 1 and 10,000 at a time.");

        var offensive = IsOffensive(kind);
        var unit = Math.Max(1, offensive ? _options.Alliances.OffensiveThugCost : _options.Alliances.DefensiveThugCost);
        var cost = unit * quantity;
        if (alliance.Treasury < cost)
            throw new GameRuleException($"{alliance.Name} holds {alliance.Treasury:C0}. That many costs {cost:C0}.");

        alliance.Treasury -= cost;
        if (offensive) alliance.OffensiveThugs += quantity;
        else alliance.DefensiveThugs += quantity;

        return cost;
    }

    /// <summary>
    /// Posts defenders from the pool to this member's house, or sends them back.
    ///
    /// Held on the member rather than shared out passively, because they are somewhere specific: a pool
    /// that defended every member at once would be six houses guarded by one set of men, and the whole
    /// point of the pool is that spending it costs somebody else the use of it.
    /// </summary>
    public async Task<(Alliance Alliance, int Posted)> PostDefendersAsync(Player member, int quantity, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(member, cancellationToken);
        return (alliance, PostDefenders(member, alliance, quantity));
    }

    /// <summary>The rule itself, over a crew already in hand. Returns what actually moved.</summary>
    public int PostDefenders(Player member, Alliance alliance, int quantity)
    {
        // Sending them back is always allowed. Somebody demoted below the threshold while holding the
        // crew's men would otherwise be unable to hand them over, which is the opposite of the point.
        if (quantity > 0)
            EnsurePower(member, alliance, AlliancePower.PostDefenders);

        if (quantity == 0)
            throw new GameRuleException("Say how many.");

        if (quantity < 0)
        {
            var released = Math.Min(-quantity, member.AllianceDefenders);
            if (released <= 0)
                throw new GameRuleException("You have none of the crew's thugs standing here.");

            member.AllianceDefenders -= released;
            alliance.DefensiveThugs += released;
            return -released;
        }

        if (alliance.DefensiveThugs < quantity)
            throw new GameRuleException($"{alliance.Name} has {alliance.DefensiveThugs:N0} defensive thug(s) spare.");

        // The same amplify-rather-than-substitute rule the raid runs under. A player cannot turn an
        // empty house into a fortress with borrowed men.
        var limit = BorrowLimit(member.Thugs);
        if (member.AllianceDefenders + quantity > limit)
            throw new GameRuleException(limit == 0
                ? "You need thugs of your own before the crew will stand with them."
                : $"You can keep {limit:N0} of the crew's thugs here, and you already have {member.AllianceDefenders:N0}.");

        alliance.DefensiveThugs -= quantity;
        member.AllianceDefenders += quantity;
        return quantity;
    }

    public static bool IsOffensive(string? kind)
        => !string.Equals(kind?.Trim(), "defensive", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Puts crews in the world for rivals to already be in.
    ///
    /// Seeded on first read rather than at sign-up, the same way ground seeds itself into an existing
    /// world: the rivals are already out there and a board with nothing but the player's own crew on it
    /// is not a board. Formed around towns because that is the alliance the world would actually make -
    /// the people who work the same streets - and it gives a player somewhere obvious to ask to join.
    ///
    /// Deliberately not every town. A world where everybody has agreed not to rob each other is a world
    /// with nothing to do, so the crews cover about half the map and the rest stay on their own.
    /// </summary>
    public async Task<int> SeedRivalCrewsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var seeds = _options.Alliances.RivalCrews;
        if (seeds.Count == 0)
            return 0;

        var existing = (await db.Alliances.AsNoTracking()
                .Select(x => x.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Only rivals, and only unaligned ones: a player is never conscripted into a crew they did not
        // ask to join. Read once and handed out from here, so two seeds cannot both claim the same name.
        var free = await db.Players
            .Include(x => x.Account)
            .Where(x => x.Account.IsBot && x.AllianceId == null)
            .OrderByDescending(economy.NetWorthExpression)
            .ToListAsync(cancellationToken);

        // Somebody has to be left to rob. A world where every rival has agreed not to fight is a world
        // with nothing in it, so a share of them stay on their own however many crews are waiting to be
        // seeded - and in a thin world that means fewer crews rather than a board of two-man alliances
        // covering everybody.
        var mustStaySolo = Math.Max(1, free.Count / 3);
        var available = Math.Max(0, free.Count - mustStaySolo);

        var seeded = 0;
        foreach (var seed in seeds)
        {
            if (existing.Contains(seed.Name))
                continue;
            if (available < 2)
                break;

            // The town is where a crew would actually form - the people working the same streets - so
            // its own rivals go in first. But a seeding rule that produced nothing in a sparsely settled
            // world would have failed at the one job it has, so a short crew is topped up from whoever
            // else is unaligned rather than abandoned.
            var take = Math.Min(available, MaxMembers - 1);
            var locals = free
                .Where(x => x.City == seed.City)
                .Concat(free.Where(x => x.City != seed.City))
                .Take(take)
                .ToList();
            if (locals.Count < 2)
                break;

            free.RemoveAll(locals.Contains);
            available -= locals.Count;

            var alliance = new Alliance
            {
                Name = seed.Name,
                Motto = seed.Motto,
                FounderId = locals[0].Id,
                DuesPercent = Math.Clamp(seed.DuesPercent, 0, _options.Alliances.MaxDuesPercent),
                // Seeded across all three, so a player meets every kind of door on their first look at
                // the board rather than discovering two of them only after building a crew of their own.
                Door = AllianceDoors.Parse(seed.Door),
                CreatedAtUtc = nowUtc
            };
            db.Alliances.Add(alliance);

            // The richest runs it, the next deputises, the rest are soldiers. A seeded crew that was
            // flat would make every rank rule in the game invisible until a player built their own.
            for (var index = 0; index < locals.Count; index++)
            {
                locals[index].Alliance = alliance;
                locals[index].AllianceJoinedAtUtc = nowUtc;
                locals[index].AllianceRank = index switch
                {
                    0 => AllianceRank.Boss,
                    1 => AllianceRank.Underboss,
                    2 => AllianceRank.Enforcer,
                    _ => AllianceRank.Soldier
                };
            }

            existing.Add(seed.Name);
            seeded++;
        }

        if (seeded > 0)
            await db.SaveChangesAsync(cancellationToken);

        return seeded;
    }

    /// <summary>Loads the caller's crew with its roster, or explains that they have not got one.</summary>
    public async Task<Alliance> LoadForAsync(Player player, CancellationToken cancellationToken)
        => player.AllianceId is not { } id
            ? throw new GameRuleException("You are not running with a crew.")
            : await db.Alliances
                  .Include(x => x.Members)
                  .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
              ?? throw new GameRuleException("You are not running with a crew.");

    /// <summary>Hands a member's posted defenders back to the pool they came out of.</summary>
    private static void ReturnDefenders(Player member, Alliance alliance)
    {
        if (member.AllianceDefenders <= 0) return;
        alliance.DefensiveThugs += member.AllianceDefenders;
        member.AllianceDefenders = 0;
    }

    /// <summary>
    /// Takes a price out of the bank first, then cash on hand. The same rule every hideout purchase
    /// runs under, and for the same reason: earnings above the safe are swept to the bank, so the bank
    /// is where the money for anything large actually is.
    /// </summary>
    private static void ChargeCapital(Player player, long cost)
    {
        var fromBank = Math.Min(player.BankCash, cost);
        player.BankCash -= fromBank;
        player.Cash -= cost - fromBank;
    }
}
