using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveCombatMissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CombatMissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttackerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    TurnsSpent = table.Column<int>(type: "integer", nullable: false),
                    AssignedPimps = table.Column<int>(type: "integer", nullable: false),
                    AssignedThugs = table.Column<int>(type: "integer", nullable: false),
                    AssignedWeapons = table.Column<int>(type: "integer", nullable: false),
                    RemainingAttackers = table.Column<int>(type: "integer", nullable: false),
                    RemainingWeapons = table.Column<int>(type: "integer", nullable: false),
                    AttackerMorale = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    DefenderMorale = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    CurrentRound = table.Column<int>(type: "integer", nullable: false),
                    MaxRounds = table.Column<int>(type: "integer", nullable: false),
                    AttackerPower = table.Column<int>(type: "integer", nullable: false),
                    DefenderPower = table.Column<int>(type: "integer", nullable: false),
                    CashStolen = table.Column<long>(type: "bigint", nullable: false),
                    WeedStolen = table.Column<int>(type: "integer", nullable: false),
                    CokeStolen = table.Column<int>(type: "integer", nullable: false),
                    AttackerPimpsLost = table.Column<int>(type: "integer", nullable: false),
                    AttackerHoesLost = table.Column<int>(type: "integer", nullable: false),
                    AttackerThugsLost = table.Column<int>(type: "integer", nullable: false),
                    AttackerWeaponsLost = table.Column<int>(type: "integer", nullable: false),
                    DefenderPimpsLost = table.Column<int>(type: "integer", nullable: false),
                    DefenderHoesLost = table.Column<int>(type: "integer", nullable: false),
                    DefenderThugsLost = table.Column<int>(type: "integer", nullable: false),
                    DefenderWeaponsLost = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArrivesAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextRoundAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DefenderProtectionUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatMissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatMissions_Players_AttackerId",
                        column: x => x.AttackerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CombatMissions_Players_DefenderId",
                        column: x => x.DefenderId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CombatMissionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CombatMissionId = table.Column<long>(type: "bigint", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    AttackRoll = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: false),
                    DefenseRoll = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: false),
                    AttackerMorale = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    DefenderMorale = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    AttackerThugsLost = table.Column<int>(type: "integer", nullable: false),
                    DefenderThugsLost = table.Column<int>(type: "integer", nullable: false),
                    AttackerWeaponsLost = table.Column<int>(type: "integer", nullable: false),
                    DefenderWeaponsLost = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatMissionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatMissionEvents_CombatMissions_CombatMissionId",
                        column: x => x.CombatMissionId,
                        principalTable: "CombatMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombatMissionEvents_CombatMissionId_CreatedAtUtc",
                table: "CombatMissionEvents",
                columns: new[] { "CombatMissionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CombatMissions_AttackerId_Status",
                table: "CombatMissions",
                columns: new[] { "AttackerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CombatMissions_DefenderId_Status",
                table: "CombatMissions",
                columns: new[] { "DefenderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CombatMissions_Status_ArrivesAtUtc_NextRoundAtUtc_ReturnsAt~",
                table: "CombatMissions",
                columns: new[] { "Status", "ArrivesAtUtc", "NextRoundAtUtc", "ReturnsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombatMissionEvents");

            migrationBuilder.DropTable(
                name: "CombatMissions");
        }
    }
}
