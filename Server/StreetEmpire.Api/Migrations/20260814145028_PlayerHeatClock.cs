using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerHeatClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeatRollUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                // Existing players start their heat clock now. The generated default was year one,
                // which would have handed everyone already playing a million-hour catch-up roll.
                defaultValueSql: "now() at time zone 'utc'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHeatRollUtc",
                table: "Players");
        }
    }
}
