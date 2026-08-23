using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// Reading a room and saying something in it.
///
/// Which room is asked for by name and answered against the player, never the other way round: there
/// is no shape of request here that reads a crew you are not in or a town you are not standing in,
/// because the scope is taken off the player rather than out of the query string.
/// </summary>
internal static class ChatEndpoints
{
    internal static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/chat", async (
            string? channel,
            CurrentPlayerService current,
            ChatService chat,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var asked = ChatChannels.Parse(channel);
            var messages = await chat.ReadAsync(player, asked, ct);

            // Swept here rather than on a timer: the table only grows when somebody is talking, and
            // somebody reading it is the cheapest moment to find that out.
            await chat.PruneAsync(DateTime.UtcNow, ct);

            return Results.Ok(new ChatBoardResponse(
                asked.ToString(),
                Scope(asked, player),
                ChatChannels.All.Select(x => Describe(x, player)).ToList(),
                messages.Select(x => ToResponse(x, player)).ToList(),
                gameOptions.Value.Chat.MaxLength));
        }).RequireAuthorization();

        app.MapPost("/api/game/chat", async (
            PostChatRequest request,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var asked = ChatChannels.Parse(request.Channel);
                var message = await chat.PostAsync(player, asked, request.Body, DateTime.UtcNow, ct);
                return Results.Ok(ToResponse(message, player));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/game/chat/conversations", async (
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var list = await chat.ConversationsAsync(player, ct);
            return Results.Ok(new ChatConversationListResponse(
                list.Select(x => new ChatConversationSummaryResponse(
                    x.Id, x.Name, x.IsGroup, x.Others, x.LastBody, x.SentAtUtc, x.Unread)).ToList(),
                list.Sum(x => x.Unread)));
        }).RequireAuthorization();

        app.MapGet("/api/game/chat/conversations/{id:long}", async (
            long id,
            CurrentPlayerService current,
            ChatService chat,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var (conversation, messages, others) = await chat.ReadConversationAsync(player, id, ct);
                return Results.Ok(new ChatConversationResponse(
                    conversation.Id,
                    conversation.IsGroup
                        ? conversation.Title ?? string.Join(", ", others)
                        : others.FirstOrDefault() ?? "Someone",
                    conversation.IsGroup,
                    others,
                    messages.Select(x => ToResponse(x, player)).ToList(),
                    gameOptions.Value.Chat.MaxLength));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        // Opening a conversation is separate from writing in it, so a window can be opened from a
        // profile or the picker without putting a word in it first.
        app.MapPost("/api/game/chat/conversations/direct", async (
            BlockRequest request,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();
            if (request.PlayerId is not { } other)
                return Results.BadRequest(new { error = "Say who you are writing to." });

            try
            {
                var conversation = await chat.OpenDirectAsync(player, other, ct);
                return Results.Ok(new { id = conversation.Id });
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/chat/conversations/group", async (
            StartGroupRequest request,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var conversation = await chat.StartGroupAsync(
                    player, request.PlayerIds ?? [], request.Title, ct);
                return Results.Ok(new { id = conversation.Id });
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/chat/conversations/{id:long}/say", async (
            long id,
            PostChatRequest request,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var message = await chat.SendAsync(player, id, request.Body, DateTime.UtcNow, ct);
                return Results.Ok(ToResponse(message, player));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/game/chat/people", async (
            string? q,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var found = await chat.SearchPeopleAsync(player, q, ct);
            return Results.Ok(new PeopleSearchResponse(
                found.Select(x => new PersonResponse(x.Id, x.Name, x.City)).ToList()));
        }).RequireAuthorization();

        app.MapGet("/api/game/chat/blocked", async (
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var blocked = await chat.BlockedAsync(player, ct);
            return Results.Ok(new BlockedListResponse(
                blocked.Select(x => new BlockedPlayerResponse(x.Id, x.Name)).ToList()));
        }).RequireAuthorization();

        app.MapPost("/api/game/chat/block", async (
            BlockRequest request,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();
            if (request.PlayerId is not { } target)
                return Results.BadRequest(new { error = "Say who." });

            try
            {
                await chat.BlockAsync(player, target, ct);
                return Results.Ok(new { blocked = true });
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/chat/unblock", async (
            BlockRequest request,
            CurrentPlayerService current,
            ChatService chat,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();
            if (request.PlayerId is not { } target)
                return Results.BadRequest(new { error = "Say who." });

            await chat.UnblockAsync(player, target, ct);
            return Results.Ok(new { blocked = false });
        }).RequireAuthorization();
    }

    private static ChatMessageResponse ToResponse(ChatMessage message, Player viewer)
        => new(
            message.Id,
            message.AuthorName,
            message.AuthorId == viewer.Id,
            message.Body,
            message.CreatedAtUtc);

    /// <summary>What the room is called for this player: the town they are in, or the crew's name.</summary>
    private static string Scope(ChatChannel channel, Player player) => channel switch
    {
        ChatChannel.City => player.City,
        ChatChannel.Alliance => player.Alliance?.Name ?? "No crew",
        _ => "Everybody"
    };

    /// <summary>
    /// Why a room is closed, worked out here rather than guessed at by the page. A tab that looks live
    /// and then refuses is worse than one that says what is missing.
    /// </summary>
    private static ChatChannelResponse Describe(ChatChannel channel, Player player)
    {
        var blocked = channel == ChatChannel.Alliance && player.AllianceId is null
            ? "You are not running with a crew."
            : null;

        return new ChatChannelResponse(
            channel.ToString(),
            ChatChannels.Label(channel),
            ChatChannels.Describe(channel),
            blocked is null,
            blocked);
    }
}
