using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class RaidsWreckRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CokeLabWreckedAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IntelligenceWreckedAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LookoutWreckedAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepairCompletesAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepairingRoom",
                table: "Hideouts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeedLabWreckedAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkshopWreckedAtUtc",
                table: "Hideouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenderRoomWrecked",
                table: "CombatMissions",
                type: "character varying(96)",
                maxLength: 96,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenderRoomWrecked",
                table: "CombatLogs",
                type: "character varying(96)",
                maxLength: 96,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CokeLabWreckedAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "IntelligenceWreckedAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "LookoutWreckedAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "RepairCompletesAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "RepairingRoom",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "WeedLabWreckedAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "WorkshopWreckedAtUtc",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "DefenderRoomWrecked",
                table: "CombatMissions");

            migrationBuilder.DropColumn(
                name: "DefenderRoomWrecked",
                table: "CombatLogs");
        }
    }
}
