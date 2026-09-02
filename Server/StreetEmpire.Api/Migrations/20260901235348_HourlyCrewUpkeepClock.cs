using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class HourlyCrewUpkeepClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpkeepUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                // Existing players start their upkeep clock now. Back-billing every account for the
                // whole age of the database would empty shelves and zero morale on deploy.
                defaultValueSql: "now() at time zone 'utc'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpkeepUtc",
                table: "Players");
        }
    }
}
