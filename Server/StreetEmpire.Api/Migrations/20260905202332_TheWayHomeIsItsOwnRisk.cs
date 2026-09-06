using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class TheWayHomeIsItsOwnRisk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ReturnRiskPercent",
                table: "PendingStrikes",
                type: "double precision",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "SeizedHoes",
                table: "PendingStrikes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeizedRides",
                table: "PendingStrikes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnRiskPercent",
                table: "PendingStrikes");

            migrationBuilder.DropColumn(
                name: "SeizedHoes",
                table: "PendingStrikes");

            migrationBuilder.DropColumn(
                name: "SeizedRides",
                table: "PendingStrikes");
        }
    }
}
