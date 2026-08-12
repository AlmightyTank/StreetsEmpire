namespace StreetEmpire.Api.Services;

/// <summary>
/// Remembers when combat next needs attention, so the resolver can skip the database entirely on the
/// many requests where nothing is due. A client polling an active mission hits five endpoints that all
/// resolve; without this gate each one pays for two queries and a turn through the resolution lock.
///
/// The stored time may be earlier than the true next event but must never be later, or resolution
/// would stall. Launching lowers it; a completed pass recomputes it exactly; cancelling can only push
/// the true event further out, so leaving the cache early after a cancel just costs one wasted pass.
/// </summary>
public sealed class CombatSchedule
{
    private long _nextDueTicks = DateTime.MinValue.Ticks;

    /// <summary>True when something may be due, meaning the caller should take the slow path.</summary>
    public bool MayBeDue(DateTime nowUtc)
        => nowUtc.Ticks >= Interlocked.Read(ref _nextDueTicks);

    /// <summary>Records the exact next event, or nothing further to do.</summary>
    public void SetNextDue(DateTime? nextDueUtc)
        => Interlocked.Exchange(ref _nextDueTicks, (nextDueUtc ?? DateTime.MaxValue).Ticks);

    /// <summary>Brings the next event forward when new work lands sooner than the cached time.</summary>
    public void NoteUpcoming(DateTime dueUtc)
    {
        var candidate = dueUtc.Ticks;
        while (true)
        {
            var current = Interlocked.Read(ref _nextDueTicks);
            if (candidate >= current)
                return;
            if (Interlocked.CompareExchange(ref _nextDueTicks, candidate, current) == current)
                return;
        }
    }

    /// <summary>Forces the next call to take the slow path.</summary>
    public void Invalidate()
        => Interlocked.Exchange(ref _nextDueTicks, DateTime.MinValue.Ticks);
}
