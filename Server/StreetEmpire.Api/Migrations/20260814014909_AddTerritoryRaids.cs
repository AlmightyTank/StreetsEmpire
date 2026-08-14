using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryRaids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TerritoryId",
                table: "CombatMissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatMissions_TerritoryId",
                table: "CombatMissions",
                column: "TerritoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_CombatMissions_Territories_TerritoryId",
                table: "CombatMissions",
                column: "TerritoryId",
                principalTable: "Territories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CombatMissions_Territories_TerritoryId",
                table: "CombatMissions");

            migrationBuilder.DropIndex(
                name: "IX_CombatMissions_TerritoryId",
                table: "CombatMissions");

            migrationBuilder.DropColumn(
                name: "TerritoryId",
                table: "CombatMissions");
        }
    }
}
