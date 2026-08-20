using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<PlayerAccount> Accounts => Set<PlayerAccount>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<GameActionLog> ActionLogs => Set<GameActionLog>();
    public DbSet<CombatLog> CombatLogs => Set<CombatLog>();
    public DbSet<CombatMission> CombatMissions => Set<CombatMission>();
    public DbSet<CombatMissionEvent> CombatMissionEvents => Set<CombatMissionEvent>();
    public DbSet<Hideout> Hideouts => Set<Hideout>();
    public DbSet<Pimp> Pimps => Set<Pimp>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<GameSetting> GameSettings => Set<GameSetting>();
    public DbSet<StandingSnapshot> StandingSnapshots => Set<StandingSnapshot>();
    public DbSet<Territory> Territories => Set<Territory>();
    public DbSet<MarketListing> MarketListings => Set<MarketListing>();
    public DbSet<MuleRun> MuleRuns => Set<MuleRun>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Alliance> Alliances => Set<Alliance>();
    public DbSet<AllianceRequest> AllianceRequests => Set<AllianceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerAccount>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(32);
            entity.HasOne(x => x.Player)
                .WithOne(x => x.Account)
                .HasForeignKey<Player>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(32);
            // The rack, and the total across it, are views over the four gun columns rather than columns.
            entity.Ignore(x => x.Armoury);
            entity.Ignore(x => x.Weapons);
            entity.Property(x => x.HoeHappiness).HasPrecision(5, 2);
            entity.Property(x => x.ThugHappiness).HasPrecision(5, 2);
        });

        modelBuilder.Entity<Alliance>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(32);
            entity.Property(x => x.Motto).HasMaxLength(140);
            // Losing a crew must not take its members with it. They come out of it unaligned, which is
            // exactly what disbanding one means.
            entity.HasMany(x => x.Members)
                .WithOne(x => x.Alliance!)
                .HasForeignKey(x => x.AllianceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AllianceRequest>(entity =>
        {
            // Every read is "what is outstanding for this crew" or "for this player", and the pair with
            // the direction is what stops the same ask being sent twice.
            entity.HasIndex(x => new { x.AllianceId, x.Kind });
            entity.HasIndex(x => new { x.PlayerId, x.Kind });
            entity.Property(x => x.Note).HasMaxLength(140);
            entity.HasOne(x => x.Alliance)
                .WithMany(x => x.Requests)
                .HasForeignKey(x => x.AllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            // A player leaving the game takes their outstanding asks with them.
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Hideout>(entity =>
        {
            entity.HasIndex(x => x.PlayerId).IsUnique();
            entity.HasOne(x => x.Player)
                .WithOne(x => x.Hideout)
                .HasForeignKey<Hideout>(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MarketListing>(entity =>
        {
            // Browsing is always "what is open, cheapest first for one good".
            entity.HasIndex(x => new { x.Item, x.PricePerUnit });
            entity.HasIndex(x => x.SellerId);
            entity.Property(x => x.Item).HasMaxLength(16);
            entity.HasOne(x => x.Seller)
                .WithMany()
                .HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MuleRun>(entity =>
        {
            // Every read is "what has this player got out", and the settler asks "what is due now".
            entity.HasIndex(x => new { x.PlayerId, x.SettledAtUtc });
            entity.HasIndex(x => x.ReturnsAtUtc);
            entity.Property(x => x.Good).HasMaxLength(16);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.Outcome).HasMaxLength(16);
            entity.Property(x => x.OriginCity).HasMaxLength(64);
            entity.Property(x => x.DestinationCity).HasMaxLength(64);
            entity.Property(x => x.PimpName).HasMaxLength(64);
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            // A pimp who defects or dies is deleted; the run that says so must outlive them.
            entity.HasOne(x => x.Pimp)
                .WithMany()
                .HasForeignKey(x => x.PimpId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            // Every read is "what is open in this town", which is exactly this index.
            entity.HasIndex(x => new { x.City, x.FilledAtUtc, x.ExpiresAtUtc });
            entity.Property(x => x.City).HasMaxLength(64);
            entity.Property(x => x.Buyer).HasMaxLength(64);
            entity.Property(x => x.Good).HasMaxLength(16);
            // A filled contract outlives the empire that filled it, so the board can still say who did.
            entity.HasOne(x => x.FilledBy)
                .WithMany()
                .HasForeignKey(x => x.FilledById)
                .OnDelete(DeleteBehavior.SetNull);
            // A claim outlives its claimant too, and the order simply frees up: SetNull hands a
            // half-delivered order back to the town rather than leaving it locked to a deleted player.
            entity.HasOne(x => x.ClaimedBy)
                .WithMany()
                .HasForeignKey(x => x.ClaimedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Territory>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(48);
            entity.Property(x => x.City).HasMaxLength(32);
            entity.Property(x => x.Type).HasMaxLength(16);
            entity.HasIndex(x => x.HolderId);
            // Losing the pimp leaves the ground held by thugs alone rather than deleting the row.
            entity.HasOne(x => x.GarrisonPimp)
                .WithMany()
                .HasForeignKey(x => x.GarrisonPimpId)
                .OnDelete(DeleteBehavior.SetNull);
            // Losing a player must not delete the ground. It goes back to being unheld.
            entity.HasOne(x => x.Holder)
                .WithMany()
                .HasForeignKey(x => x.HolderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StandingSnapshot>(entity =>
        {
            // Every read is "the sample nearest a moment, for one player" or "the whole sample at a
            // moment", so both orderings are worth indexing.
            entity.HasIndex(x => new { x.PlayerId, x.TakenAtUtc });
            entity.HasIndex(x => x.TakenAtUtc);
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameSetting>(entity =>
        {
            entity.Property(x => x.MaintenanceMessage).HasMaxLength(400);
            entity.Property(x => x.Announcement).HasMaxLength(400);
            entity.Property(x => x.UpdatedBy).HasMaxLength(32);
            // Seeded so the single row always exists and readers never have to cope with its absence.
            entity.HasData(new GameSetting { Id = 1, UpdatedAtUtc = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc) });
        });

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.TargetPlayerId);
            entity.Property(x => x.ActorUsername).HasMaxLength(32);
            entity.Property(x => x.Action).HasMaxLength(32);
            entity.Property(x => x.TargetName).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(400);
            entity.Property(x => x.Reason).HasMaxLength(400);
        });

        modelBuilder.Entity<PlayerAccount>(entity =>
        {
            entity.Property(x => x.EnforcementReason).HasMaxLength(400);
        });

        modelBuilder.Entity<Pimp>(entity =>
        {
            entity.HasIndex(x => new { x.PlayerId, x.LostAtUtc });
            entity.Property(x => x.Name).HasMaxLength(48);
            entity.Property(x => x.LostReason).HasMaxLength(32);
            entity.Property(x => x.Loyalty).HasPrecision(5, 2);
            entity.HasOne(x => x.Player)
                .WithMany(x => x.Crew)
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameActionLog>(entity =>
        {
            entity.HasIndex(x => new { x.PlayerId, x.CreatedAtUtc });
            entity.Property(x => x.Action).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(800);
        });

        modelBuilder.Entity<CombatLog>(entity =>
        {
            entity.HasIndex(x => new { x.AttackerId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.DefenderId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.Outcome, x.ResolvesAtUtc });
            entity.Property(x => x.Method).HasMaxLength(16);
            entity.Property(x => x.Outcome).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(800);
            entity.HasOne(x => x.Attacker)
                .WithMany(x => x.AttacksMade)
                .HasForeignKey(x => x.AttackerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Defender)
                .WithMany(x => x.Defenses)
                .HasForeignKey(x => x.DefenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CombatMission>(entity =>
        {
            // The rack a crew is carrying is a view over the four Carried* columns, not a column itself.
            entity.Ignore(x => x.Carried);
            entity.HasIndex(x => new { x.AttackerId, x.Status });
            entity.HasIndex(x => new { x.DefenderId, x.Status });
            entity.HasIndex(x => new { x.Status, x.ArrivesAtUtc, x.NextRoundAtUtc, x.ReturnsAtUtc });
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Outcome).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(800);
            entity.Property(x => x.AttackerMorale).HasPrecision(5, 2);
            entity.Property(x => x.DefenderMorale).HasPrecision(5, 2);
            // The ground outliving the raid matters: deleting a territory must not take its history.
            entity.HasOne(x => x.Territory)
                .WithMany()
                .HasForeignKey(x => x.TerritoryId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Attacker)
                .WithMany(x => x.MissionsStarted)
                .HasForeignKey(x => x.AttackerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Defender)
                .WithMany(x => x.MissionsDefended)
                .HasForeignKey(x => x.DefenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.CommanderName).HasMaxLength(48);
            // A dead commander's row stays for the roll of the fallen, so the mission keeps pointing at it.
            entity.HasOne(x => x.CommanderPimp)
                .WithMany()
                .HasForeignKey(x => x.CommanderPimpId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CombatMissionEvent>(entity =>
        {
            entity.HasIndex(x => new { x.CombatMissionId, x.CreatedAtUtc });
            entity.Property(x => x.Kind).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(800);
            entity.Property(x => x.AttackRoll).HasPrecision(8, 2);
            entity.Property(x => x.DefenseRoll).HasPrecision(8, 2);
            entity.Property(x => x.AttackerMorale).HasPrecision(5, 2);
            entity.Property(x => x.DefenderMorale).HasPrecision(5, 2);
            entity.HasOne(x => x.CombatMission)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.CombatMissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
