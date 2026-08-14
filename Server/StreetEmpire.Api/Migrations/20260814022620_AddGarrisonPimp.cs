using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGarrisonPimp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GarrisonPimpId",
                table: "Territories",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Territories_GarrisonPimpId",
                table: "Territories",
                column: "GarrisonPimpId");

            migrationBuilder.AddForeignKey(
                name: "FK_Territories_Pimps_GarrisonPimpId",
                table: "Territories",
                column: "GarrisonPimpId",
                principalTable: "Pimps",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Territories_Pimps_GarrisonPimpId",
                table: "Territories");

            migrationBuilder.DropIndex(
                name: "IX_Territories_GarrisonPimpId",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "GarrisonPimpId",
                table: "Territories");
        }
    }
}
