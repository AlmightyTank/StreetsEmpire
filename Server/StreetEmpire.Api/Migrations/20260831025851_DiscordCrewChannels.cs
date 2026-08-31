using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class DiscordCrewChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordCrewChannelMapJson",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordCrewChannelsSyncedAtUtc",
                table: "GameSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DiscordCrewChannelMapJson", "DiscordCrewChannelsSyncedAtUtc" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordCrewChannelMapJson",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordCrewChannelsSyncedAtUtc",
                table: "GameSettings");
        }
    }
}
