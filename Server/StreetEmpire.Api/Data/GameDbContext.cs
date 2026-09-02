using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<PlayerAccount> Accounts => Set<PlayerAccount>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<PlayerSession> Sessions => Set<PlayerSession>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<BetaKey> BetaKeys => Set<BetaKey>();
    public DbSet<HideoutIntel> HideoutIntel => Set<HideoutIntel>();
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
    public DbSet<Arrest> Arrests => Set<Arrest>();
    public DbSet<WorkshopCraft> WorkshopCrafts => Set<WorkshopCraft>();
    public DbSet<TraderJob> TraderJobs => Set<TraderJob>();
    public DbSet<TraderStock> TraderStocks => Set<TraderStock>();
    public DbSet<TraderJobLead> TraderJobLeads => Set<TraderJobLead>();
    public DbSet<Alliance> Alliances => Set<Alliance>();
    public DbSet<AllianceRequest> AllianceRequests => Set<AllianceRequest>();
    public DbSet<AlliancePact> AlliancePacts => Set<AlliancePact>();
    public DbSet<AllianceWar> AllianceWars => Set<AllianceWar>();
    public DbSet<AllianceAssistCall> AllianceAssistCalls => Set<AllianceAssistCall>();
    public DbSet<AllianceTransfer> AllianceTransfers => Set<AllianceTransfer>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PlayerBlock> PlayerBlocks => Set<PlayerBlock>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<GameAnnouncement> GameAnnouncements => Set<GameAnnouncement>();
    public DbSet<CustomTitle> CustomTitles => Set<CustomTitle>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<SeasonResult> SeasonResults => Set<SeasonResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerAccount>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(32);
            // Both of the other ways in are unique for the same reason the username is: they are things
            // you sign in as, so two accounts holding one would make the lookup ambiguous and the answer
            // to "whose account is this" a matter of row order.
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.HasIndex(x => x.DiscordUserId).IsUnique();
            entity.Property(x => x.DiscordUserId).HasMaxLength(32);
            entity.Property(x => x.DiscordUsername).HasMaxLength(64);
            entity.Property(x => x.DiscordAvatarHash).HasMaxLength(128);
            entity.Property(x => x.CustomAvatarContentType).HasMaxLength(32);
            entity.Property(x => x.AvatarSource).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ProfileTagline).HasMaxLength(140);
            entity.Property(x => x.ProfilePronouns).HasMaxLength(64);
            entity.Property(x => x.ProfileLocation).HasMaxLength(64);
            entity.Property(x => x.ProfileAccent).HasConversion<string>().HasMaxLength(16).HasDefaultValue(ProfileAccent.Gold);
            entity.Property(x => x.ProfileBanner).HasConversion<string>().HasMaxLength(16).HasDefaultValue(ProfileBanner.None);
            entity.Property(x => x.FeaturedTitle).HasMaxLength(32);
            // 24 rather than 16: AllianceAndPacts is sixteen characters exactly, and a column sized to
            // the longest value it has ever held is a column that refuses the next one.
            entity.Property(x => x.DirectMessagePolicy).HasConversion<string>().HasMaxLength(24);

            // Said here as well as on the property, and that is not belt and braces. The C# initialiser
            // decides what a new account gets; this decides what the rows that already exist get when
            // the column is added. Left off, EF scaffolds default(bool) - and the first deploy would
            // quietly switch every existing player's alerts off and hide their profile activity, with
            // nothing to see and nobody told.
            entity.Property(x => x.ShowActivityOnProfile).HasDefaultValue(true);
            entity.Property(x => x.NoticeCombat).HasDefaultValue(true);
            entity.Property(x => x.NoticeCrew).HasDefaultValue(true);
            entity.Property(x => x.NoticeMarket).HasDefaultValue(true);
            entity.Property(x => x.EmailSecurityNotices).HasDefaultValue(true);
            entity.Property(x => x.EmailCombatNotices).HasDefaultValue(true);
            entity.Property(x => x.EmailAllianceNotices).HasDefaultValue(true);
            entity.Property(x => x.DiscordSecurityNotices).HasDefaultValue(false);
            entity.Property(x => x.DiscordCombatNotices).HasDefaultValue(false);
            entity.Property(x => x.DiscordCrewNotices).HasDefaultValue(false);
            entity.Property(x => x.DiscordMarketNotices).HasDefaultValue(false);
            entity.Ignore(x => x.HasPassword);
            entity.HasOne(x => x.Player)
                .WithOne(x => x.Account)
                .HasForeignKey<Player>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerSession>(entity =>
        {
            entity.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Every read of this table is "the sessions belonging to one account" - the page listing
            // them, and the sweep clearing them out.
            entity.HasIndex(x => x.AccountId);

            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.UserAgent).HasMaxLength(256);
        });

        modelBuilder.Entity<CustomTitle>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(32);
            entity.Property(x => x.Title).HasMaxLength(64);
            entity.Property(x => x.Detail).HasMaxLength(240);
            entity.Property(x => x.Criteria).HasMaxLength(32);
            entity.Property(x => x.TextValue).HasMaxLength(64);
            entity.Property(x => x.CreatedByUsername).HasMaxLength(32);
            entity.Property(x => x.UpdatedByUsername).HasMaxLength(32);
        });

        modelBuilder.Entity<HideoutIntel>(entity =>
        {
            // One row per pair, overwritten each time somebody looks again. The unique index is the rule
            // rather than a hint: two rows for one pair would be two answers to a question with one.
            entity.HasIndex(x => new { x.ViewerId, x.SubjectId }).IsUnique();

            entity.HasOne(x => x.Viewer)
                .WithMany()
                .HasForeignKey(x => x.ViewerId)
                .OnDelete(DeleteBehavior.Cascade);

            // The subject's deletion must not cascade into the viewer's rows twice over - Postgres
            // refuses two cascade paths to the same table - so the notes somebody kept on a player who
            // has left are cleared rather than cascaded.
            entity.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RecoveryCode>(entity =>
        {
            entity.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Every read is "the unused codes for this account", which is both what redeeming walks and
            // what the page counts.
            entity.HasIndex(x => new { x.AccountId, x.UsedAtUtc });
            entity.Property(x => x.CodeHash).HasMaxLength(256);
        });

        modelBuilder.Entity<BetaKey>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.IssuedToAccountId);
            entity.HasIndex(x => x.RedeemedByAccountId);
            entity.Property(x => x.Code).HasMaxLength(32);
            entity.Property(x => x.Label).HasMaxLength(120);
            entity.Property(x => x.Version).IsConcurrencyToken();

            entity.HasOne(x => x.IssuedToAccount)
                .WithMany()
                .HasForeignKey(x => x.IssuedToAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RedeemedByAccount)
                .WithMany()
                .HasForeignKey(x => x.RedeemedByAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailVerification>(entity =>
        {
            // Every read is "the newest code of this kind for this account", so that is the index.
            entity.HasIndex(x => new { x.AccountId, x.Purpose, x.CreatedAtUtc });
            // Swept by age, so the sweep gets an index of its own rather than a table scan a day.
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.Property(x => x.Email).HasMaxLength(254);
            // Stored as its name. An integer here would mean reading the source to find out what a row
            // in the table is for, and this is a table somebody reads while something is going wrong.
            entity.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.SealedCode).HasMaxLength(512);
            entity.HasOne(x => x.Account)
                .WithMany(x => x.EmailVerifications)
                .HasForeignKey(x => x.AccountId)
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

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasIndex(x => x.Number).IsUnique();
            // Only ever one running, and the question "which season is this" is asked on every read.
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.Name).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(16);
        });

        modelBuilder.Entity<SeasonResult>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.Rank });
            entity.HasIndex(x => x.PlayerId);
            entity.Property(x => x.PlayerName).HasMaxLength(32);
            entity.Property(x => x.City).HasMaxLength(32);
            entity.Property(x => x.CrewName).HasMaxLength(48);
            entity.Property(x => x.Honour).HasMaxLength(24);
            entity.HasOne(x => x.Season)
                .WithMany()
                .HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
            // A result outlives the account it belongs to being deleted only if the row itself goes
            // with it: an honours table full of players nobody can look up is worse than a shorter one.
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AllianceWar>(entity =>
        {
            // Both directions indexed on status, because every question asked of this table is "is
            // this crew at war" and a crew is as often the one declared on as the one declaring.
            entity.HasIndex(x => new { x.DeclaringAllianceId, x.Status });
            entity.HasIndex(x => new { x.TargetAllianceId, x.Status });
            // The settle sweep reads this one on its own: every war whose clock has run out.
            entity.HasIndex(x => new { x.Status, x.EndsAtUtc });
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.Outcome).HasMaxLength(400);
            entity.HasOne(x => x.DeclaringAlliance)
                .WithMany()
                .HasForeignKey(x => x.DeclaringAllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TargetAlliance)
                .WithMany()
                .HasForeignKey(x => x.TargetAllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            // The declarer is kept, not cascaded: a settled war is a record of what happened between
            // two crews, and losing the player who started it must not erase it.
            entity.HasOne(x => x.DeclaredBy)
                .WithMany()
                .HasForeignKey(x => x.DeclaredById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlliancePact>(entity =>
        {
            entity.HasIndex(x => new { x.RequestingAllianceId, x.TargetAllianceId, x.Status });
            entity.HasIndex(x => new { x.TargetAllianceId, x.Status });
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.HasOne(x => x.RequestingAlliance)
                .WithMany()
                .HasForeignKey(x => x.RequestingAllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TargetAlliance)
                .WithMany()
                .HasForeignKey(x => x.TargetAllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RequestedBy)
                .WithMany()
                .HasForeignKey(x => x.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AnsweredBy)
                .WithMany()
                .HasForeignKey(x => x.AnsweredById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AllianceAssistCall>(entity =>
        {
            entity.HasIndex(x => new { x.AllyAllianceId, x.Status });
            entity.HasIndex(x => new { x.DefenderAllianceId, x.Status });
            entity.HasIndex(x => new { x.CombatMissionId, x.AllyAllianceId }).IsUnique();
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.HasOne(x => x.CombatMission)
                .WithMany()
                .HasForeignKey(x => x.CombatMissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefenderAlliance)
                .WithMany()
                .HasForeignKey(x => x.DefenderAllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AllyAlliance)
                .WithMany()
                .HasForeignKey(x => x.AllyAllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RespondedBy)
                .WithMany()
                .HasForeignKey(x => x.RespondedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AllianceTransfer>(entity =>
        {
            entity.HasIndex(x => new { x.AllianceId, x.CreatedAtUtc });
            entity.HasIndex(x => x.ToPlayerId);
            entity.Property(x => x.Item).HasMaxLength(16);
            entity.HasOne(x => x.Alliance)
                .WithMany()
                .HasForeignKey(x => x.AllianceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FromPlayer)
                .WithMany()
                .HasForeignKey(x => x.FromPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToPlayer)
                .WithMany()
                .HasForeignKey(x => x.ToPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Hideout>(entity =>
        {
            entity.HasIndex(x => x.PlayerId).IsUnique();
            // A room key, so it is short and it is one of a fixed set. Bounded for the same reason
            // every other key column here is: a string column with no length is a column somebody can
            // eventually write a paragraph into.
            entity.Property(x => x.RepairingRoom).HasMaxLength(16);
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

        modelBuilder.Entity<Arrest>(entity =>
        {
            // Every read is "who is this player holding in a cell", and the settler asks "whose time
            // is up". The same pair of indexes a mule run is read by, for the same two questions.
            entity.HasIndex(x => new { x.PlayerId, x.SettledAtUtc });
            entity.HasIndex(x => x.BailDeadlineUtc);
            entity.Property(x => x.Outcome).HasMaxLength(16);
            entity.Property(x => x.City).HasMaxLength(64);
            entity.Property(x => x.District).HasMaxLength(32);
            entity.Property(x => x.PimpName).HasMaxLength(64);
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            // The pimp is nulled rather than the row going with them, because the row is the record of
            // what happened and outlives whoever it happened to. PimpName is kept for that reason.
            entity.HasOne(x => x.Pimp)
                .WithMany()
                .HasForeignKey(x => x.PimpId)
                .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<WorkshopCraft>(entity =>
        {
            entity.HasIndex(x => new { x.PlayerId, x.CompletedAtUtc });
            entity.HasIndex(x => x.CompletesAtUtc);
            entity.Property(x => x.Good).HasMaxLength(16);
            entity.Property(x => x.Label).HasMaxLength(32);
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TraderJob>(entity =>
        {
            // Every read of this table is the same question: what is going in this town, soonest
            // deadline first. One book where there were two, so one index instead of two identical ones.
            entity.HasIndex(x => new { x.City, x.FilledAtUtc, x.ExpiresAtUtc });
            entity.Property(x => x.City).HasMaxLength(64);
            entity.Property(x => x.Good).HasMaxLength(16);
            entity.Property(x => x.OnBehalfOf).HasMaxLength(64);
            entity.HasOne(x => x.FilledBy)
                .WithMany()
                .HasForeignKey(x => x.FilledById)
                .OnDelete(DeleteBehavior.SetNull);
            // A claim outlives its claimant and the job simply frees up, rather than staying locked to
            // somebody who no longer exists. That hands a half-delivered job back to the town rather
            // than leaving it stuck to a deleted player.
            entity.HasOne(x => x.ClaimedBy)
                .WithMany()
                .HasForeignKey(x => x.ClaimedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TraderStock>(entity =>
        {
            // One row per line per town, and the database says so: two rows for the same shelf would be
            // two answers to how many are left, and a purchase would take from whichever was read first.
            entity.HasIndex(x => new { x.City, x.Good }).IsUnique();
            entity.Property(x => x.City).HasMaxLength(64);
            entity.Property(x => x.Good).HasMaxLength(16);
        });

        modelBuilder.Entity<TraderJobLead>(entity =>
        {
            // One row per slot per town per player, and the database says so rather than the service
            // hoping: a duplicate slot would be a hand that deals the same job twice and a reroll that
            // swaps one of them.
            entity.HasIndex(x => new { x.PlayerId, x.City, x.Slot }).IsUnique();
            entity.Property(x => x.City).HasMaxLength(64);
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            // A hand is a pointer at the town's book, so a job leaving takes its leads with it. Nobody
            // should be holding a slot pointing at a row that no longer exists.
            entity.HasOne(x => x.Job)
                .WithMany(x => x.Leads)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<ChatMessage>(entity =>
        {
            // Every read is "the newest lines in one room", and the room is the channel plus its scope.
            entity.HasIndex(x => new { x.Channel, x.City, x.Id });
            entity.HasIndex(x => new { x.Channel, x.AllianceId, x.Id });
            // The rate limit asks "has this person spoken lately", which is this.
            entity.HasIndex(x => new { x.AuthorId, x.CreatedAtUtc });
            // Every conversation read is "the newest in this one", which is this.
            entity.HasIndex(x => new { x.ConversationId, x.Id });
            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.City).HasMaxLength(64);
            entity.Property(x => x.AuthorName).HasMaxLength(32);
            entity.Property(x => x.Body).HasMaxLength(400);
            // A line outlives the person who said it: the name is already kept beside it, so losing the
            // author should blank the link rather than delete what they said.
            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlayerBlock>(entity =>
        {
            // One block per pair per direction, enforced rather than checked: blocking somebody twice
            // is a double-click, not a decision, and a duplicate row would make unblocking a lottery.
            entity.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();
            // Read from both ends on every chat read: who I have silenced, and who has silenced me.
            entity.HasIndex(x => x.BlockedId);
            entity.HasOne(x => x.Blocker)
                .WithMany()
                .HasForeignKey(x => x.BlockerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Blocked)
                .WithMany()
                .HasForeignKey(x => x.BlockedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationMember>(entity =>
        {
            // One membership per person per conversation, and the two ways it is read: who is in this
            // conversation, and which conversations is this person in.
            entity.HasIndex(x => new { x.ConversationId, x.PlayerId }).IsUnique();
            entity.HasIndex(x => x.PlayerId);
            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(48);
            entity.HasIndex(x => x.LastMessageAtUtc);
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
            entity.Property(x => x.DiscordAnnouncementWebhookUrl).HasMaxLength(512);
            entity.Property(x => x.DiscordAnnouncementUsername).HasMaxLength(80);
            entity.Property(x => x.DiscordBotToken).HasMaxLength(256);
            entity.Property(x => x.DiscordApplicationId).HasMaxLength(32);
            entity.Property(x => x.DiscordPublicKey).HasMaxLength(128);
            entity.Property(x => x.DiscordGuildId).HasMaxLength(32);
            entity.Property(x => x.DiscordLinkedRoleId).HasMaxLength(32);
            entity.Property(x => x.DiscordTopTenRoleId).HasMaxLength(32);
            entity.Property(x => x.DiscordCrewBossRoleId).HasMaxLength(32);
            entity.Property(x => x.DiscordCrewRoleMapJson);
            entity.Property(x => x.DiscordTitleRoleMapJson);
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

        modelBuilder.Entity<GameAnnouncement>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(96);
            entity.Property(x => x.Category).HasMaxLength(24);
            entity.Property(x => x.Severity).HasMaxLength(24).HasDefaultValue("Info");
            entity.Property(x => x.Version).HasMaxLength(32);
            entity.Property(x => x.ActionLabel).HasMaxLength(40);
            entity.Property(x => x.ActionUrl).HasMaxLength(240);
            entity.Property(x => x.CreatedByUsername).HasMaxLength(32);
            entity.Property(x => x.UpdatedByUsername).HasMaxLength(32);
            entity.HasIndex(x => new { x.IsDraft, x.ArchivedAtUtc, x.IsPinned, x.PublishedAtUtc })
                .HasDatabaseName("IX_GameAnnouncements_VisibleFeed");
            entity.HasIndex(x => x.ExpiresAtUtc);
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
            // Room keys joined by commas, and there are five rooms that can break.
            entity.Property(x => x.DefenderRoomWrecked).HasMaxLength(96);
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
            entity.Property(x => x.DefenderRoomWrecked).HasMaxLength(96);
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
