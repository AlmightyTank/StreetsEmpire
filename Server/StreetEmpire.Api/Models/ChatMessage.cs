namespace StreetEmpire.Api.Models;

/// <summary>
/// One thing somebody said.
///
/// The scope is stored beside the channel rather than worked out from the author when the message is
/// read, and that is the whole design: a message said in Detroit stays a Detroit message after its
/// author has moved to Miami, and a message said to a crew stays with that crew after its author has
/// left it. Reading the author's current state instead would quietly rewrite history every time
/// somebody travelled or walked out.
/// </summary>
public sealed class ChatMessage
{
    public long Id { get; set; }

    public ChatChannel Channel { get; set; }

    /// <summary>The town this was said in, for city messages. Null on every other channel.</summary>
    public string? City { get; set; }

    /// <summary>The crew this was said to, for crew messages. Null on every other channel.</summary>
    public long? AllianceId { get; set; }

    public Guid? AuthorId { get; set; }
    public Player? Author { get; set; }

    /// <summary>
    /// The name as it was at the time. Kept alongside the author rather than joined for on every read:
    /// a player who is gone still said what they said, and the line should not turn into "Someone" or
    /// vanish because the row it pointed at did.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// The conversation this belongs to, for anything that is not a room. Null on the three channels.
    ///
    /// This began as a recipient, with a thread worked out by folding the pair together. That shape
    /// could not hold a third person: nothing to fold, no way to say who is in a conversation before
    /// anybody has spoken, nowhere to hang a name. The conversation is a row of its own now and a
    /// direct message is one with two people in it.
    /// </summary>
    public long? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
