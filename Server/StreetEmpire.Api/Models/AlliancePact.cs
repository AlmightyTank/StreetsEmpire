namespace StreetEmpire.Api.Models;

public sealed class AlliancePact
{
    public long Id { get; set; }

    public long RequestingAllianceId { get; set; }
    public Alliance RequestingAlliance { get; set; } = null!;

    public long TargetAllianceId { get; set; }
    public Alliance TargetAlliance { get; set; } = null!;

    public Guid RequestedById { get; set; }
    public Player RequestedBy { get; set; } = null!;

    public Guid? AnsweredById { get; set; }
    public Player? AnsweredBy { get; set; }

    public string Status { get; set; } = AlliancePactStatuses.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAtUtc { get; set; }
}

public static class AlliancePactStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Declined = "Declined";
    public const string Canceled = "Canceled";
}
