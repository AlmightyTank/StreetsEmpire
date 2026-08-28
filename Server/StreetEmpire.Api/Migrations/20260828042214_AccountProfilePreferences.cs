using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountProfilePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailAllianceNotices",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailCombatNotices",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailSecurityNotices",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileAccent",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Gold");

            migrationBuilder.AddColumn<string>(
                name: "ProfileLocation",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePronouns",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SyncDiscordAvatar",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailAllianceNotices",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "EmailCombatNotices",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "EmailSecurityNotices",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ProfileAccent",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ProfileLocation",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ProfilePronouns",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SyncDiscordAvatar",
                table: "Accounts");
        }
    }
}
