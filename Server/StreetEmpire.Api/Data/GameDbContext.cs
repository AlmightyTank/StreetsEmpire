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
            entity.Property(x => x.HoeHappiness).HasPrecision(5, 2);
            entity.Property(x => x.ThugHappiness).HasPrecision(5, 2);
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
            entity.HasIndex(x => new { x.AttackerId, x.Status });
            entity.HasIndex(x => new { x.DefenderId, x.Status });
            entity.HasIndex(x => new { x.Status, x.ArrivesAtUtc, x.NextRoundAtUtc, x.ReturnsAtUtc });
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Outcome).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(800);
            entity.Property(x => x.AttackerMorale).HasPrecision(5, 2);
            entity.Property(x => x.DefenderMorale).HasPrecision(5, 2);
            entity.HasOne(x => x.Attacker)
                .WithMany(x => x.MissionsStarted)
                .HasForeignKey(x => x.AttackerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Defender)
                .WithMany(x => x.MissionsDefended)
                .HasForeignKey(x => x.DefenderId)
                .OnDelete(DeleteBehavior.Restrict);
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
