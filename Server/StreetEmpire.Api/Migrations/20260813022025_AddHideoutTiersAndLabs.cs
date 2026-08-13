using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHideoutTiersAndLabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Left null rather than backfilled to now: null means "the labs have never run", and the
            // first refresh starts their clock. Backfilling would work too, but null also covers the
            // player who has no lab yet and would otherwise carry a meaningless timestamp.
            migrationBuilder.AddColumn<DateTime>(
                name: "LabsCollectedAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpgradeCompletesAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpgradingToTier",
                table: "Hideouts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabsCollectedAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "UpgradeCompletesAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "UpgradingToTier",
                table: "Hideouts");
        }
    }
}
