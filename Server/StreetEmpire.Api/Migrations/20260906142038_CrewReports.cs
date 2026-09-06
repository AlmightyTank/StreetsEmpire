using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class CrewReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CrewReportsPostedAtUtc",
                table: "GameSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "CrewReportsPostedAtUtc",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrewReportsPostedAtUtc",
                table: "GameSettings");
        }
    }
}
