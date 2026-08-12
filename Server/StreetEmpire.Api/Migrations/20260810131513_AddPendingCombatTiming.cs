using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCombatTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "CombatLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvesAtUtc",
                table: "CombatLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatLogs_Outcome_ResolvesAtUtc",
                table: "CombatLogs",
                columns: new[] { "Outcome", "ResolvesAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CombatLogs_Outcome_ResolvesAtUtc",
                table: "CombatLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "CombatLogs");

            migrationBuilder.DropColumn(
                name: "ResolvesAtUtc",
                table: "CombatLogs");
        }
    }
}
