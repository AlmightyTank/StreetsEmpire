namespace StreetEmpire.Api.Models;

/// <summary>
/// A conversation: the people in it, and everything said between them.
///
/// Direct messages were built as a pair - a message with a recipient, and a thread worked out by
/// folding those together. That was the right shape for two people and does not survive three: there
/// is no pair to fold, no way to say who is in a conversation before anybody has spoken, and nowhere
/// to hang a name. So the conversation is a thing now, and a direct message is simply one with two
/// people in it. One mechanism rather than two that drift.
/// </summary>
public sealed class Conversation
{
    public long Id { get; set; }

    /// <summary>
    /// Whether this is a named group or a pair. A pair has no title and is found by who is in it, so
    /// writing to the same person twice reopens the conversation rather than starting a second one.
    /// </summary>
    public bool IsGroup { get; set; }

    /// <summary>What the group is called. Null for a pair, which is named after whoever else is in it.</summary>
    public string? Title { get; set; }

    public Guid? CreatedById { get; set; }
    public Player? CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When anything was last said, so a list can be ordered without reading every message.</summary>
    public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;

    public List<ConversationMember> Members { get; set; } = [];
}

/// <summary>
/// Somebody in a conversation, and how far they have read.
///
/// Membership is what decides who may read and write, which is the whole security model here: there is
/// no query in the service that reaches a conversation the asker is not a member of.
/// </summary>
public sealed class ConversationMember
{
    public long Id { get; set; }

    public long ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>
    /// The last message this person has seen. An actual watermark rather than the guess the pair-based
    /// version used, which counted anything newer than your own last reply and so called a conversation
    /// unread forever if you never answered it.
    /// </summary>
    public long LastReadMessageId { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
