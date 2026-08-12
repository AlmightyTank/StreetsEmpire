using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPimpSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BonusPercent",
                table: "Pimps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "Pimps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CommanderBonusPercent",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Pimps hired before specialties existed would otherwise sit on an empty specialty and a
            // zero bonus, which reads as broken. Alternate within each roster so every player has a
            // mix to choose between, and spread the bonus across the 3-8 range new hires roll.
            migrationBuilder.Sql("""
                UPDATE "Pimps" AS p
                SET "Specialty" = CASE WHEN seated.seat % 2 = 1 THEN 'Enforcer' ELSE 'Hustler' END,
                    "BonusPercent" = 3 + (seated.seat % 6)
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "PlayerId" ORDER BY "Id") AS seat
                    FROM "Pimps"
                ) AS seated
                WHERE seated."Id" = p."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BonusPercent",
                table: "Pimps");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "Pimps");

            migrationBuilder.DropColumn(
                name: "CommanderBonusPercent",
                table: "CombatMissions");
        }
    }
}
