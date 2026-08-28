using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class CustomAvatarsAndTaglines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CustomAvatar",
                table: "Accounts",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomAvatarContentType",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomAvatarUpdatedAtUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileTagline",
                table: "Accounts",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomAvatar",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CustomAvatarContentType",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CustomAvatarUpdatedAtUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ProfileTagline",
                table: "Accounts");
        }
    }
}
