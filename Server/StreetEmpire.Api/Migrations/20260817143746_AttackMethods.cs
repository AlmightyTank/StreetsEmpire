using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AttackMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Medicine",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rides",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StrikeProtectionUntilUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HoesTaken",
                table: "CombatLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Every fight that already happened was a raid, because it was the only attack there was.
            // Defaulted and backfilled rather than left empty so the history reads correctly instead of
            // relying on every consumer treating a blank method as a raid.
            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "CombatLogs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "raid");

            migrationBuilder.Sql("UPDATE \"CombatLogs\" SET \"Method\" = 'raid' WHERE \"Method\" = '';");

            migrationBuilder.AddColumn<int>(
                name: "RidesTaken",
                table: "CombatLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Medicine",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Rides",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "StrikeProtectionUntilUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HoesTaken",
                table: "CombatLogs");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "CombatLogs");

            migrationBuilder.DropColumn(
                name: "RidesTaken",
                table: "CombatLogs");
        }
    }
}
