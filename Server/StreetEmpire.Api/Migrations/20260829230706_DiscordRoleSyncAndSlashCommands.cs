using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class DiscordRoleSyncAndSlashCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordApplicationId",
                table: "GameSettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordBotToken",
                table: "GameSettings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordCityRoleMapJson",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordCommandsRegisteredAtUtc",
                table: "GameSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordCrewBossRoleId",
                table: "GameSettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordGuildId",
                table: "GameSettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordLinkedRoleId",
                table: "GameSettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordPublicKey",
                table: "GameSettings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordRolesSyncedAtUtc",
                table: "GameSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordTopTenRoleId",
                table: "GameSettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DiscordApplicationId", "DiscordBotToken", "DiscordCityRoleMapJson", "DiscordCommandsRegisteredAtUtc", "DiscordCrewBossRoleId", "DiscordGuildId", "DiscordLinkedRoleId", "DiscordPublicKey", "DiscordRolesSyncedAtUtc", "DiscordTopTenRoleId" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordApplicationId",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordBotToken",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordCityRoleMapJson",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordCommandsRegisteredAtUtc",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordCrewBossRoleId",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordGuildId",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordLinkedRoleId",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordPublicKey",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordRolesSyncedAtUtc",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordTopTenRoleId",
                table: "GameSettings");
        }
    }
}
