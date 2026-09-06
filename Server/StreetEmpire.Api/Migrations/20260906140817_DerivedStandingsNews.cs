using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class DerivedStandingsNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CrewRanksJson",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JackpotAnnouncedAmount",
                table: "GameSettings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleHoldersJson",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CrewRanksJson", "JackpotAnnouncedAmount", "TitleHoldersJson" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrewRanksJson",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "JackpotAnnouncedAmount",
                table: "GameSettings");

            migrationBuilder.DropColumn(
                name: "TitleHoldersJson",
                table: "GameSettings");
        }
    }
}
