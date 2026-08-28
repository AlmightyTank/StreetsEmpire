using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Talking.
///
/// Three rooms, one table. What decides which room a message lands in is worked out here rather than
/// trusted from the request: a client asking to post to a crew is asked which crew only so it can be
/// refused if it is not theirs, and the scope actually written is read off the player.
/// </summary>
public sealed class ChatService(GameDbContext db, IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// What a player can see in one room. Newest first, capped, and scoped by who is asking - there is
    /// no request shape that reads another crew's room.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> ReadAsync(
        Player player,
        ChatChannel channel,
        CancellationToken cancellationToken)
    {
        var config = _options.Chat;
        var query = db.ChatMessages.AsNoTracking();

        query = channel switch
        {
            ChatChannel.City => query.Where(x => x.Channel == ChatChannel.City && x.City == player.City),
            // A player with no crew has no crew room. Returning an empty list rather than everything is
            // the difference between "nothing here" and "here is somebody else's".
            ChatChannel.Alliance => player.AllianceId is { } id
                ? query.Where(x => x.Channel == ChatChannel.Alliance && x.AllianceId == id)
                : query.Where(x => false),
            _ => query.Where(x => x.Channel == ChatChannel.Global)
        };

        // Fetch a little deeper than the page, because some of it is about to be dropped: a room where
        // half the voices are silenced should still hand back a full screen of the ones that are not.
        var depth = Math.Clamp(config.HistoryDepth, 10, 200);
        var silenced = await SilencedAsync(player, cancellationToken);
        var messages = await query
            .OrderByDescending(x => x.Id)
            .Take(silenced.Count > 0 ? depth * 3 : depth)
            .ToListAsync(cancellationToken);

        if (silenced.Count > 0)
            messages = messages.Where(x => x.AuthorId is not { } id || !silenced.Contains(id)).ToList();

        // Oldest first for reading, newest first for fetching: the database should not sort the whole
        // table to hand back fifty rows.
        messages = messages.Take(depth).ToList();
        messages.Reverse();
        return messages;
    }

    /// <summary>
    /// Says something, or explains why not.
    ///
    /// Every refusal here is about the player rather than the words: whether they may speak in this
    /// room at all, and whether they have spoken too recently. What the words are is a length check and
    /// nothing more - the client renders text, never markup, so there is no injection to defend against
    /// and no reason to be inventive about what a person is allowed to type.
    /// </summary>
    public async Task<ChatMessage> PostAsync(
        Player player,
        ChatChannel channel,
        string? body,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var text = Clean(body);

        if (channel == ChatChannel.Alliance && player.AllianceId is null)
            throw new GameRuleException("You are not running with a crew.");

        await EnsurePaceAsync(player, nowUtc, cancellationToken);

        var message = new ChatMessage
        {
            Channel = channel,
            City = channel == ChatChannel.City ? player.City : null,
            AllianceId = channel == ChatChannel.Alliance ? player.AllianceId : null,
            AuthorId = player.Id,
            AuthorName = player.Name,
            Body = text,
            CreatedAtUtc = nowUtc
        };

        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    /// <summary>
    /// Every conversation this player is in, newest first.
    ///
    /// Membership decides it, which is the whole of the security model: there is no argument to this
    /// that reaches a conversation somebody is not in.
    /// </summary>
    public async Task<IReadOnlyList<ChatConversationSummary>> ConversationsAsync(
        Player player,
        CancellationToken cancellationToken)
    {
        var silenced = await SilencedAsync(player, cancellationToken);

        var mine = await db.ConversationMembers
            .AsNoTracking()
            .Where(x => x.PlayerId == player.Id)
            .Select(x => new { x.ConversationId, x.LastReadMessageId })
            .ToListAsync(cancellationToken);
        if (mine.Count == 0) return [];

        var ids = mine.Select(x => x.ConversationId).ToList();

        var conversations = await db.Conversations
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Include(x => x.Members)
            .OrderByDescending(x => x.LastMessageAtUtc)
            .ToListAsync(cancellationToken);

        // Folded to a flat list of ids first. Left inline, the lambda closed over the conversations
        // already in memory and walked a navigation property inside a database filter, which EF cannot
        // turn into SQL - so every read of this list threw rather than returning it.
        var memberIds = conversations
            .SelectMany(x => x.Members)
            .Select(x => x.PlayerId)
            .Distinct()
            .ToList();

        var names = await db.Players
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var lastBodies = await db.ChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId != null && ids.Contains(x.ConversationId!.Value))
            .GroupBy(x => x.ConversationId!.Value)
            .Select(g => new { Id = g.Key, Last = g.Max(m => m.Id) })
            .ToListAsync(cancellationToken);

        var lastIds = lastBodies.Select(x => x.Last).ToList();
        var lastMessages = await db.ChatMessages
            .AsNoTracking()
            .Where(x => lastIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.ConversationId!.Value, x => x, cancellationToken);

        var unreadCounts = await db.ChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId != null && ids.Contains(x.ConversationId!.Value) && x.AuthorId != player.Id)
            .Select(x => new { Conversation = x.ConversationId!.Value, x.Id })
            .ToListAsync(cancellationToken);

        var watermarks = mine.ToDictionary(x => x.ConversationId, x => x.LastReadMessageId);

        return conversations.Select(conversation =>
        {
            var others = conversation.Members
                .Where(m => m.PlayerId != player.Id)
                .Select(m => names.TryGetValue(m.PlayerId, out var name) ? name : "Someone")
                .ToList();

            lastMessages.TryGetValue(conversation.Id, out var last);
            var watermark = watermarks.TryGetValue(conversation.Id, out var mark) ? mark : 0;

            return new ChatConversationSummary(
                conversation.Id,
                Name(conversation, others),
                conversation.IsGroup,
                others,
                // A silenced voice does not get to be the preview line either.
                last is not null && last.AuthorId is { } who && silenced.Contains(who) ? string.Empty : last?.Body ?? string.Empty,
                conversation.LastMessageAtUtc,
                unreadCounts.Count(x => x.Conversation == conversation.Id && x.Id > watermark));
        }).ToList();
    }

    /// <summary>What a conversation is called: its title, or whoever else is in it.</summary>
    private static string Name(Conversation conversation, IReadOnlyList<string> others)
        => conversation.IsGroup
            ? string.IsNullOrWhiteSpace(conversation.Title)
                ? others.Count > 0 ? string.Join(", ", others) : "Group"
                : conversation.Title!
            : others.FirstOrDefault() ?? "Someone";

    /// <summary>
    /// Reads one, and marks it read to the end.
    ///
    /// Membership is checked first and separately: a conversation somebody is not in should refuse
    /// rather than come back empty, because an empty one reads as "nothing said yet" and invites
    /// another go.
    /// </summary>
    public async Task<(Conversation Conversation, IReadOnlyList<ChatMessage> Messages, IReadOnlyList<string> Others)> ReadConversationAsync(
        Player player,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var membership = await db.ConversationMembers
            .SingleOrDefaultAsync(x => x.ConversationId == conversationId && x.PlayerId == player.Id, cancellationToken)
            ?? throw new GameRuleException("There is nothing to read here.");

        var conversation = await db.Conversations
            .Include(x => x.Members)
            .SingleAsync(x => x.Id == conversationId, cancellationToken);

        var silenced = await SilencedAsync(player, cancellationToken);

        var depth = Math.Clamp(_options.Chat.HistoryDepth, 10, 200);
        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.Id)
            .Take(silenced.Count > 0 ? depth * 3 : depth)
            .ToListAsync(cancellationToken);

        if (silenced.Count > 0)
            messages = messages.Where(x => x.AuthorId is not { } id || !silenced.Contains(id)).ToList();
        messages = messages.Take(depth).ToList();
        messages.Reverse();

        // Read to the end, so the badge means something rather than counting forever.
        var newest = messages.Count > 0 ? messages[^1].Id : membership.LastReadMessageId;
        if (newest > membership.LastReadMessageId)
        {
            membership.LastReadMessageId = newest;
            await db.SaveChangesAsync(cancellationToken);
        }

        var otherIds = conversation.Members.Where(m => m.PlayerId != player.Id).Select(m => m.PlayerId).ToList();
        var others = await db.Players
            .AsNoTracking()
            .Where(x => otherIds.Contains(x.Id))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        return (conversation, messages, others);
    }

    /// <summary>
    /// Opens the conversation with one person, making it if this is the first word between them.
    ///
    /// The same pair always lands in the same conversation - writing to somebody twice reopens what
    /// you had rather than starting a second one beside it, which is the difference between a
    /// messages list and a pile of duplicates.
    /// </summary>
    public async Task<Conversation> OpenDirectAsync(Player player, Guid otherId, CancellationToken cancellationToken)
    {
        if (otherId == player.Id)
            throw new GameRuleException("You are already talking to yourself.");

        var other = await db.Players
            .Include(x => x.Account)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == otherId, cancellationToken)
            ?? throw new GameRuleException("There is nobody by that name.");

        var silenced = await SilencedAsync(player, cancellationToken);
        if (silenced.Contains(otherId))
            throw new GameRuleException($"{other.Name} is not taking messages from you.");
        var pactAllies = await DirectMessages.PactAlliesAsync(db, player.AllianceId, cancellationToken);
        if (DirectMessageBlockReason(player, other, pactAllies) is { } privacy)
            throw new GameRuleException(privacy);

        // The pair conversation is the one that is not a group and has exactly these two in it.
        var existing = await db.Conversations
            .Where(x => !x.IsGroup
                && x.Members.Count == 2
                && x.Members.Any(m => m.PlayerId == player.Id)
                && x.Members.Any(m => m.PlayerId == otherId))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var conversation = new Conversation
        {
            IsGroup = false,
            CreatedById = player.Id,
            CreatedAtUtc = DateTime.UtcNow,
            LastMessageAtUtc = DateTime.UtcNow,
            Members =
            [
                new ConversationMember { PlayerId = player.Id },
                new ConversationMember { PlayerId = otherId }
            ]
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    /// <summary>
    /// Starts a group. Anybody who has silenced the person starting it is left out rather than
    /// quietly added: a block should not be walked around by putting somebody in a room.
    /// </summary>
    public async Task<Conversation> StartGroupAsync(
        Player player,
        IReadOnlyList<Guid> memberIds,
        string? title,
        CancellationToken cancellationToken)
    {
        var wanted = memberIds.Where(x => x != player.Id).Distinct().ToList();
        if (wanted.Count == 0)
            throw new GameRuleException("Pick somebody to talk to.");

        var max = Math.Max(2, _options.Chat.MaxGroupMembers);
        if (wanted.Count + 1 > max)
            throw new GameRuleException($"A group holds {max} people. That is {wanted.Count + 1}.");

        var silenced = await SilencedAsync(player, cancellationToken);
        var allowed = wanted.Where(x => !silenced.Contains(x)).ToList();
        if (allowed.Count == 0)
            throw new GameRuleException("Nobody you picked is taking messages from you.");

        var real = await db.Players
            .Include(x => x.Account)
            .AsNoTracking()
            .Where(x => allowed.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var pactAllies = await DirectMessages.PactAlliesAsync(db, player.AllianceId, cancellationToken);
        var takingMessages = real
            .Where(x => DirectMessageBlockReason(player, x, pactAllies) is null)
            .Select(x => x.Id)
            .ToList();
        if (takingMessages.Count == 0)
            throw new GameRuleException(real.Count == 0
                ? "There is nobody by that name."
                : "Nobody you picked is taking messages from you.");

        var clean = (title ?? string.Empty).Trim();
        if (clean.Length > 48)
            throw new GameRuleException("Keep the name under 48 characters.");

        var conversation = new Conversation
        {
            IsGroup = true,
            Title = clean.Length == 0 ? null : clean,
            CreatedById = player.Id,
            CreatedAtUtc = DateTime.UtcNow,
            LastMessageAtUtc = DateTime.UtcNow,
            Members = takingMessages.Append(player.Id)
                .Select(id => new ConversationMember { PlayerId = id })
                .ToList()
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    /// <summary>
    /// Says something in a conversation. Membership decides whether it lands, so a group behaves as a
    /// pair does and neither needs a rule of its own.
    /// </summary>
    public async Task<ChatMessage> SendAsync(
        Player player,
        long conversationId,
        string? body,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new GameRuleException("There is nothing to read here.");

        var membership = conversation.Members.SingleOrDefault(x => x.PlayerId == player.Id)
            ?? throw new GameRuleException("There is nothing to read here.");

        // A pair where the other end has stopped listening.
        if (!conversation.IsGroup)
        {
            var otherId = conversation.Members.Single(x => x.PlayerId != player.Id).PlayerId;
            var silenced = await SilencedAsync(player, cancellationToken);
            if (silenced.Contains(otherId))
                throw new GameRuleException("They are not taking messages from you.");
            var other = await db.Players
                .Include(x => x.Account)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == otherId, cancellationToken)
                ?? throw new GameRuleException("There is nothing to read here.");
            var pactAllies = await DirectMessages.PactAlliesAsync(db, player.AllianceId, cancellationToken);
            if (DirectMessageBlockReason(player, other, pactAllies) is { } privacy)
                throw new GameRuleException(privacy);
        }

        var text = Clean(body);
        await EnsurePaceAsync(player, nowUtc, cancellationToken);

        var message = new ChatMessage
        {
            Channel = ChatChannel.Direct,
            ConversationId = conversation.Id,
            AuthorId = player.Id,
            AuthorName = player.Name,
            Body = text,
            CreatedAtUtc = nowUtc
        };
        db.ChatMessages.Add(message);
        conversation.LastMessageAtUtc = nowUtc;
        await db.SaveChangesAsync(cancellationToken);

        // Your own words are read by definition.
        membership.LastReadMessageId = message.Id;
        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    /// <summary>
    /// Finding somebody to write to.
    ///
    /// Excludes the searcher and anybody either of them has silenced, so the picker never offers a
    /// conversation that would be refused the moment it was opened.
    /// </summary>
    public async Task<IReadOnlyList<(Guid Id, string Name, string City)>> SearchPeopleAsync(
        Player player,
        string? term,
        CancellationToken cancellationToken)
    {
        var text = (term ?? string.Empty).Trim();
        if (text.Length < 2) return [];

        var silenced = await SilencedAsync(player, cancellationToken);

        var found = await db.Players
            .Include(x => x.Account)
            .AsNoTracking()
            .Where(x => x.Id != player.Id && EF.Functions.ILike(x.Name, $"%{text}%"))
            .OrderBy(x => x.Name)
            .Take(20)
            .ToListAsync(cancellationToken);

        var pactAllies = await DirectMessages.PactAlliesAsync(db, player.AllianceId, cancellationToken);
        return found
            .Where(x => !silenced.Contains(x.Id) && DirectMessageBlockReason(player, x, pactAllies) is null)
            .Select(x => (x.Id, x.Name, x.City))
            .ToList();
    }

    /// <summary>
    /// Why this send is refused, or null. The rule is DirectMessages', shared with the mapper that
    /// decides whether the button is drawn at all, so the page and the server cannot disagree about
    /// whether a door is open.
    /// </summary>
    /// Takes the pact set rather than loading it, because the callers here check a list of players
    /// against one sender, and a query per row would be the same answer fifty times over.
    public static string? DirectMessageBlockReason(
        Player sender, Player recipient, IReadOnlySet<long> senderPactAllies)
        => DirectMessages.BlockedReason(sender, recipient, senderPactAllies);

    /// <summary>
    /// Everybody this player will not hear from, in either direction.
    ///
    /// Both directions, because a block is a wall rather than a filter: somebody you have silenced
    /// cannot reach you, and you cannot read them either. A block that let the blocker keep watching
    /// would be surveillance dressed up as a refusal.
    /// </summary>
    private async Task<HashSet<Guid>> SilencedAsync(Player player, CancellationToken cancellationToken)
    {
        var pairs = await db.PlayerBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == player.Id || x.BlockedId == player.Id)
            .Select(x => new { x.BlockerId, x.BlockedId })
            .ToListAsync(cancellationToken);

        return pairs
            .Select(x => x.BlockerId == player.Id ? x.BlockedId : x.BlockerId)
            .ToHashSet();
    }

    /// <summary>Stops hearing from somebody. Silences them; it does not shield you from them.</summary>
    public async Task BlockAsync(Player player, Guid otherId, CancellationToken cancellationToken)
    {
        if (otherId == player.Id)
            throw new GameRuleException("You cannot block yourself.");

        var exists = await db.Players.AsNoTracking().AnyAsync(x => x.Id == otherId, cancellationToken);
        if (!exists)
            throw new GameRuleException("There is nobody by that name.");

        var already = await db.PlayerBlocks
            .AnyAsync(x => x.BlockerId == player.Id && x.BlockedId == otherId, cancellationToken);
        if (already) return;

        db.PlayerBlocks.Add(new PlayerBlock
        {
            BlockerId = player.Id,
            BlockedId = otherId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Takes it back. Only your own block: theirs is theirs.</summary>
    public async Task UnblockAsync(Player player, Guid otherId, CancellationToken cancellationToken)
    {
        await db.PlayerBlocks
            .Where(x => x.BlockerId == player.Id && x.BlockedId == otherId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Who this player has silenced, for a list they can undo.</summary>
    public async Task<IReadOnlyList<(Guid Id, string Name)>> BlockedAsync(Player player, CancellationToken cancellationToken)
        => (await db.PlayerBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == player.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.BlockedId, Name = x.Blocked!.Name })
            .ToListAsync(cancellationToken))
            .Select(x => (x.BlockedId, x.Name))
            .ToList();

    /// <summary>
    /// The words, checked once for every channel there is. Length and whitespace and nothing else - the
    /// client renders text and never markup, so there is no injection to defend against and no reason
    /// to be inventive about what somebody is allowed to type.
    /// </summary>
    private string Clean(string? body)
    {
        var text = (body ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new GameRuleException("Say something first.");
        if (text.Length > _options.Chat.MaxLength)
            throw new GameRuleException($"Keep it under {_options.Chat.MaxLength} characters. That was {text.Length:N0}.");

        // Newlines are collapsed rather than refused: somebody pasting a wall is not doing anything
        // wrong, they just should not be able to take over the panel with whitespace.
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// One quiet moment between messages, wherever they are going. Read off the table rather than held
    /// in memory, so it survives a restart and cannot be sidestepped by opening a second tab - and
    /// counted across every channel at once, or the limit is just an invitation to alternate.
    /// </summary>
    private async Task EnsurePaceAsync(Player player, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var seconds = Math.Max(0, _options.Chat.SecondsBetweenMessages);
        var since = nowUtc.AddSeconds(-seconds);
        var tooSoon = await db.ChatMessages
            .AsNoTracking()
            .AnyAsync(x => x.AuthorId == player.Id && x.CreatedAtUtc > since, cancellationToken);
        if (tooSoon)
            throw new GameRuleException($"Give it {seconds} second(s) between messages.");
    }

    /// <summary>
    /// Drops messages nobody is going to scroll back to.
    ///
    /// Chat is the one table in the game that grows with talking rather than with playing, so it is the
    /// one that needs sweeping. Run from the same place the board is read, because a table nobody reads
    /// does not need tidying and a table somebody is reading is one somebody is adding to.
    /// </summary>
    public async Task<int> PruneAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var days = Math.Max(1, _options.Chat.RetentionDays);
        var cutoff = nowUtc.AddDays(-days);
        return await db.ChatMessages.Where(x => x.CreatedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>A conversation in a list: what it is called, who is in it, and where it got to.</summary>
public sealed record ChatConversationSummary(
    long Id,
    string Name,
    bool IsGroup,
    IReadOnlyList<string> Others,
    string LastBody,
    DateTime SentAtUtc,
    int Unread);
