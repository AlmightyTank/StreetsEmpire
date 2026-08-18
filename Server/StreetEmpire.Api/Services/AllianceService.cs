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
public sealed class AllianceService(GameDbContext db, IOptionsSnapshot<GameOptions> options, EconomyService economy)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Whether these two have agreed not to rob each other. The single question every attack path asks,
    /// and the reason a null alliance on either side is emphatically not a match: two unaligned players
    /// are not allies, they are two people with nothing between them.
    /// </summary>
    public static bool AreAllied(Player one, Player other)
        => one.AllianceId is { } crew && other.AllianceId == crew;

    public async Task<Alliance> FoundAsync(Player founder, string? name, string? motto, DateTime nowUtc, CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(founder);
        var config = _options.Alliances;

        if (founder.AllianceId is not null)
            throw new GameRuleException("You are already running with a crew. Leave it before starting your own.");

        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 3 or > 32)
            throw new GameRuleException("A crew needs a name between 3 and 32 characters.");
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
    public async Task<Alliance> UpdateAsync(Player founder, int? duesPercent, string? door, string? motto, IReadOnlyDictionary<string, string>? powers, CancellationToken cancellationToken)
    {
        var alliance = await LoadForAsync(founder, cancellationToken);
        if (founder.AllianceRank != AllianceRank.Boss)
            throw new GameRuleException("Only the boss decides that.");

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
            foreach (var (name, rank) in powers)
                if (Enum.TryParse<AlliancePower>(name, ignoreCase: true, out var power))
                    alliance.SetMinRankFor(power, AllianceRanks.Parse(rank));

        return alliance;
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

        var crews = await db.Alliances.AsNoTracking().ToListAsync(cancellationToken);

        return crews
            .Select(x =>
            {
                var tally = totals.GetValueOrDefault(x.Id);
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
                    x.FounderId == viewer.Id);
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
            subject = await db.Players.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
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
