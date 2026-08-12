using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCombatSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CombatProtectionUntilUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttackAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttackedAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CombatLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttackerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    TurnsSpent = table.Column<int>(type: "integer", nullable: false),
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
                    DefenderProtectionUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatLogs_Players_AttackerId",
                        column: x => x.AttackerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CombatLogs_Players_DefenderId",
                        column: x => x.DefenderId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombatLogs_AttackerId_CreatedAtUtc",
                table: "CombatLogs",
                columns: new[] { "AttackerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CombatLogs_DefenderId_CreatedAtUtc",
                table: "CombatLogs",
                columns: new[] { "DefenderId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombatLogs");

            migrationBuilder.DropColumn(
                name: "CombatProtectionUntilUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastAttackAtUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastAttackedAtUtc",
                table: "Players");
        }
    }
}
