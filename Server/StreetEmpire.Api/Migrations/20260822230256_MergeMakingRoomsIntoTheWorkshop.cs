using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class MergeMakingRoomsIntoTheWorkshop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fold before dropping. A player who built a still or a mix house paid for reach, and the
            // one bench that replaces all three has to open at least as far up the list as the rooms
            // they are losing - otherwise the merge quietly confiscates what they bought.
            //
            // GREATEST rather than a sum: these were three rooms doing one job, so somebody who had all
            // three ends up with the best of them, not with a workshop past the top of the ladder.
            migrationBuilder.Sql("""
                UPDATE "Hideouts"
                SET "WorkshopLevel" = GREATEST("WorkshopLevel", "StillLevel", "MixLevel");
                """);

            migrationBuilder.DropColumn(
                name: "MixLevel",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "StillLevel",
                table: "Hideouts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MixLevel",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StillLevel",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
