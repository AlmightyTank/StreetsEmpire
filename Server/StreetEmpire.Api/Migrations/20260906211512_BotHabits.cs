using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class BotHabits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BotNeverSleeps",
                table: "Accounts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BotPeakHourUtc",
                table: "Accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BotSessionsPerDay",
                table: "Accounts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotNeverSleeps",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BotPeakHourUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BotSessionsPerDay",
                table: "Accounts");
        }
    }
}
