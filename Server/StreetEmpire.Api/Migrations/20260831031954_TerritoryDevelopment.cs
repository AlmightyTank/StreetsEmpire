using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class TerritoryDevelopment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DevelopingToLevel",
                table: "Territories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DevelopmentCompletesAtUtc",
                table: "Territories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DevelopmentLevel",
                table: "Territories",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DevelopingToLevel",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "DevelopmentCompletesAtUtc",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "DevelopmentLevel",
                table: "Territories");
        }
    }
}
