using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

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

        var messages = await query
            .OrderByDescending(x => x.Id)
            .Take(Math.Clamp(config.HistoryDepth, 10, 200))
            .ToListAsync(cancellationToken);

        // Oldest first for reading, newest first for fetching: the database should not sort the whole
        // table to hand back fifty rows.
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
        var config = _options.Chat;

        var text = (body ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new GameRuleException("Say something first.");
        if (text.Length > config.MaxLength)
            throw new GameRuleException($"Keep it under {config.MaxLength} characters. That was {text.Length:N0}.");

        // Newlines are collapsed rather than refused: somebody pasting a wall is not doing anything
        // wrong, they just should not be able to take over the panel with whitespace.
        text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (channel == ChatChannel.Alliance && player.AllianceId is null)
            throw new GameRuleException("You are not running with a crew.");

        // One quiet moment between messages. Read off the table rather than held in memory, so it
        // survives a restart and cannot be sidestepped by opening a second tab.
        var since = nowUtc.AddSeconds(-Math.Max(0, config.SecondsBetweenMessages));
        var tooSoon = await db.ChatMessages
            .AsNoTracking()
            .AnyAsync(x => x.AuthorId == player.Id && x.CreatedAtUtc > since, cancellationToken);
        if (tooSoon)
            throw new GameRuleException($"Give it {config.SecondsBetweenMessages} second(s) between messages.");

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
