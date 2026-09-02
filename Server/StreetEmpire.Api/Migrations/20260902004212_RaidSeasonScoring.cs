using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class RaidSeasonScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RaidCashTaken",
                table: "SeasonResults",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RaidCokeTaken",
                table: "SeasonResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RaidScore",
                table: "SeasonResults",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RaidWeedTaken",
                table: "SeasonResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RaidCashTaken",
                table: "SeasonResults");

            migrationBuilder.DropColumn(
                name: "RaidCokeTaken",
                table: "SeasonResults");

            migrationBuilder.DropColumn(
                name: "RaidScore",
                table: "SeasonResults");

            migrationBuilder.DropColumn(
                name: "RaidWeedTaken",
                table: "SeasonResults");
        }
    }
}
