using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCatchUpWatermark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Left null rather than backfilled. Null means "never shown a digest", which the endpoint
            // treats as the player's first arrival: it starts the watermark and says nothing, instead
            // of summarising the player's entire history back at them on the next login.
            migrationBuilder.AddColumn<DateTime>(
                name: "CatchUpSeenAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatchUpSeenAtUtc",
                table: "Players");
        }
    }
}
