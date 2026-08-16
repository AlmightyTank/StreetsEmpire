using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerFlightTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TravelArrivesAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TravelArrivesAtUtc",
                table: "Players");
        }
    }
}
