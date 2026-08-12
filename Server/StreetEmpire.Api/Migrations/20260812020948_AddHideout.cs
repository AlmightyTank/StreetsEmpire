using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHideout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hideouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    StorageLevel = table.Column<int>(type: "integer", nullable: false),
                    SafeLevel = table.Column<int>(type: "integer", nullable: false),
                    WeedLabLevel = table.Column<int>(type: "integer", nullable: false),
                    CokeLabLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hideouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hideouts_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hideouts_PlayerId",
                table: "Hideouts",
                column: "PlayerId",
                unique: true);

            // Existing players predate hideout limits, so they are grandfathered in with every room
            // already maxed. Starting them at level 1 would strand stock they legitimately earned
            // behind a 10-condom storage room. New players start at level 1 from the model defaults.
            migrationBuilder.Sql("""
                INSERT INTO "Hideouts" ("PlayerId", "Tier", "StorageLevel", "SafeLevel", "WeedLabLevel", "CokeLabLevel", "CreatedAtUtc")
                SELECT p."Id", 1, 3, 2, 0, 0, NOW() FROM "Players" AS p;
                """);

            // AI rivals are clamped to those same limits so the leaderboard and combat targets obey
            // the new rules immediately. Cash over the safe moves to the bank rather than being lost.
            // Not reversible: Down drops the table but cannot restore trimmed stock.
            migrationBuilder.Sql("""
                UPDATE "Players" AS p
                SET "BankCash" = p."BankCash" + GREATEST(0, p."Cash" - 100000),
                    "Cash"     = LEAST(p."Cash", 100000),
                    "Pimps"    = LEAST(p."Pimps", 6),
                    "Hoes"     = LEAST(p."Hoes", 50),
                    "Thugs"    = LEAST(p."Thugs", 25),
                    "Condoms"  = LEAST(p."Condoms", 84),
                    "Beer"     = LEAST(p."Beer", 50),
                    "Weapons"  = LEAST(p."Weapons", 25),
                    "Weed"     = LEAST(p."Weed", 100),
                    "Coke"     = LEAST(p."Coke", 50)
                FROM "Accounts" AS a
                WHERE a."Id" = p."AccountId" AND a."IsBot" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hideouts");
        }
    }
}
