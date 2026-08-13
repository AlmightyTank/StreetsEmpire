using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDefenceAlertWatermark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CombatAlertsSeenAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            // Existing players have already read their combat history in the log, so start their
            // watermark at now. Left null, every historical attack arrives as an unread alert and
            // greets them with a badge in the hundreds. New players start null and have no history.
            migrationBuilder.Sql("UPDATE \"Players\" SET \"CombatAlertsSeenAtUtc\" = NOW();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombatAlertsSeenAtUtc",
                table: "Players");
        }
    }
}
