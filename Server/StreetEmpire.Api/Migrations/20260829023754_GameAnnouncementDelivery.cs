using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class GameAnnouncementDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameAnnouncements_IsDraft_ArchivedAtUtc_PublishedAtUtc",
                table: "GameAnnouncements");

            migrationBuilder.AddColumn<string>(
                name: "Added",
                table: "GameAnnouncements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Changed",
                table: "GameAnnouncements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordSentAtUtc",
                table: "GameAnnouncements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fixed",
                table: "GameAnnouncements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "GameAnnouncements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KnownIssues",
                table: "GameAnnouncements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendToDiscord",
                table: "GameAnnouncements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "GameAnnouncements",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Info");

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnce",
                table: "GameAnnouncements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "GameAnnouncements",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameAnnouncements_VisibleFeed",
                table: "GameAnnouncements",
                columns: new[] { "IsDraft", "ArchivedAtUtc", "IsPinned", "PublishedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameAnnouncements_VisibleFeed",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "Added",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "Changed",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "DiscordSentAtUtc",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "Fixed",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "KnownIssues",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "SendToDiscord",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "ShowOnce",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "GameAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_GameAnnouncements_IsDraft_ArchivedAtUtc_PublishedAtUtc",
                table: "GameAnnouncements",
                columns: new[] { "IsDraft", "ArchivedAtUtc", "PublishedAtUtc" });
        }
    }
}
