using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountPrivacySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DirectMessagePolicy",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Everyone");

            migrationBuilder.AddColumn<bool>(
                name: "ShowDiscordOnProfile",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectMessagePolicy",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ShowDiscordOnProfile",
                table: "Accounts");
        }
    }
}
