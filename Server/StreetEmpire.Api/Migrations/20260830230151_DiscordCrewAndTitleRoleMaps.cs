using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class DiscordCrewAndTitleRoleMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordCrewRoleMapJson",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordTitleRoleMapJson",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DiscordCrewRoleMapJson", "DiscordTitleRoleMapJson" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordCrewRoleMapJson",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "DiscordTitleRoleMapJson",
                table: "GameSettings");
        }
    }
}
