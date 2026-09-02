using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StoreInvestmentReadyAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StoreRep",
                table: "Players",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // Everybody already playing starts standing where their rack says they stand.
            //
            // Zero for everyone would be a confiscation dressed as a feature: a player who has been
            // buying rifles all month would open the release unable to replace the ones they lose in
            // their next fight, having done nothing but keep playing. Reading it off the guns they
            // already own answers the only question that matters - could this player buy this gun
            // yesterday - without inventing a history of trade nobody recorded.
            //
            // The thresholds are the shipped ladder's, written out rather than read from configuration
            // because a migration is a thing that happened once, at these numbers, and retuning the
            // ladder later must not silently rewrite what a past release did.
            migrationBuilder.Sql("""
                UPDATE "Players" SET "StoreRep" = CASE
                    WHEN "Rifles" > 0 THEN 15000
                    WHEN "Smgs" > 0 THEN 3000
                    WHEN "Shotguns" > 0 THEN 300
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoreInvestmentReadyAtUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "StoreRep",
                table: "Players");
        }
    }
}
