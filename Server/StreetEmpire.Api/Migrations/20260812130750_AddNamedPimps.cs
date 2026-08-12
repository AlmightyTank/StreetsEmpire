using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNamedPimps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommanderName",
                table: "CombatMissions",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CommanderPimpId",
                table: "CombatMissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pimps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Loyalty = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    MissionsLed = table.Column<int>(type: "integer", nullable: false),
                    Victories = table.Column<int>(type: "integer", nullable: false),
                    HiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LostAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LostReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pimps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pimps_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombatMissions_CommanderPimpId",
                table: "CombatMissions",
                column: "CommanderPimpId");

            migrationBuilder.CreateIndex(
                name: "IX_Pimps_PlayerId_LostAtUtc",
                table: "Pimps",
                columns: new[] { "PlayerId", "LostAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_CombatMissions_Pimps_CommanderPimpId",
                table: "CombatMissions",
                column: "CommanderPimpId",
                principalTable: "Pimps",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Every existing player's pimp count becomes that many named crew, so Players."Pimps"
            // and the active rows agree from the first request. Names repeat across players, which is
            // fine: they only have to be unique within one roster. generate_series expands the count
            // into one row per pimp, and row_number pairs each with a distinct name.
            migrationBuilder.Sql("""
                WITH street_names(idx, name) AS (
                    VALUES (1,'Lil Daddy'),(2,'Silk Reno'),(3,'Big Cassius'),(4,'Papa Lux'),
                           (5,'Slim Osgood'),(6,'Duke Mercer'),(7,'Fat Solomon'),(8,'Smooth Ellis'),
                           (9,'Baby Vaughn'),(10,'Sweet Lorenzo'),(11,'King Ambrose'),(12,'Cool Rufus')
                ),
                roster AS (
                    SELECT p."Id" AS player_id,
                           ROW_NUMBER() OVER (PARTITION BY p."Id" ORDER BY slot) AS seat
                    FROM "Players" AS p
                    CROSS JOIN LATERAL generate_series(1, GREATEST(p."Pimps", 0)) AS slot
                )
                INSERT INTO "Pimps" ("PlayerId", "Name", "Loyalty", "MissionsLed", "Victories", "HiredAtUtc")
                SELECT r.player_id,
                       CASE WHEN r.seat <= 12 THEN n.name
                            ELSE n.name || ' ' || (((r.seat - 1) / 12) + 1)::text
                       END,
                       100, 0, 0, NOW()
                FROM roster AS r
                -- Wraps rather than joining on seat directly, so a roster larger than the name list
                -- still gets a row per pimp and the count invariant holds.
                JOIN street_names AS n ON n.idx = ((r.seat - 1) % 12) + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CombatMissions_Pimps_CommanderPimpId",
                table: "CombatMissions");

            migrationBuilder.DropTable(
                name: "Pimps");

            migrationBuilder.DropIndex(
                name: "IX_CombatMissions_CommanderPimpId",
                table: "CombatMissions");

            migrationBuilder.DropColumn(
                name: "CommanderName",
                table: "CombatMissions");

            migrationBuilder.DropColumn(
                name: "CommanderPimpId",
                table: "CombatMissions");
        }
    }
}
