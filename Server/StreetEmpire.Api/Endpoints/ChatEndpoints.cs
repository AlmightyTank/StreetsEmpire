using Microsoft.Extensions.Options;
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
