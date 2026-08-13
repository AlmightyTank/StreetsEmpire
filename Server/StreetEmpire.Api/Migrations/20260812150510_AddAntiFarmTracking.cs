using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAntiFarmTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefenderProtectionMinutes",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefenderRecentHits",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LootMultiplierPercent",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Missions that ran before anti-farm existed took a full haul, so record them as 100%.
            // Left at the column default of 0 they would each report "0% haul" in the client.
            migrationBuilder.Sql("""
                UPDATE "CombatMissions" SET "LootMultiplierPercent" = 100;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefenderProtectionMinutes",
                table: "CombatMissions");

            migrationBuilder.DropColumn(
                name: "DefenderRecentHits",
                table: "CombatMissions");

            migrationBuilder.DropColumn(
                name: "LootMultiplierPercent",
                table: "CombatMissions");
        }
    }
}
