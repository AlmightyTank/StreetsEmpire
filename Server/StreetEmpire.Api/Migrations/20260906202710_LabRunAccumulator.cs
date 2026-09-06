using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class LabRunAccumulator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingLabCoke",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingLabCokeSold",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "PendingLabEarned",
                table: "Hideouts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "PendingLabHours",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingLabWeed",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingLabWeedSold",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingLabCoke",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "PendingLabCokeSold",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "PendingLabEarned",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "PendingLabHours",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "PendingLabWeed",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "PendingLabWeedSold",
                table: "Hideouts");
        }
    }
}
