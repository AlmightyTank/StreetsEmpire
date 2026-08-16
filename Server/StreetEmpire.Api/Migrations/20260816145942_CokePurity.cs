using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class CokePurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CokePurity",
                table: "Players",
                type: "double precision",
                nullable: false,
                // Everything already in the world is clean. The generated default was zero, which
                // would have made every existing pile of coke worthless the moment this shipped.
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "Purity",
                table: "MarketListings",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CokePurity",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Purity",
                table: "MarketListings");
        }
    }
}
