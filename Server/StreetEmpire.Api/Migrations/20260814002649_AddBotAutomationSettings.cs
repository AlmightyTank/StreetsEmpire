using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBotAutomationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BotAutomationEnabled",
                table: "GameSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BotRoundsPerTick",
                table: "GameSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BotTickSeconds",
                table: "GameSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BotAutomationEnabled", "BotRoundsPerTick", "BotTickSeconds" },
                values: new object[] { false, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotAutomationEnabled",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "BotRoundsPerTick",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "BotTickSeconds",
                table: "GameSettings");
        }
    }
}
