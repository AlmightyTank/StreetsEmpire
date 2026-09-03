using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class LabSwitches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CokeLabAutoSell",
                table: "Hideouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CokeLabRunning",
                table: "Hideouts",
                type: "boolean",
                nullable: false,
                // On, for every house that already exists. EF writes false here because it reads the
                // column and not the property initialiser, and false would switch off every lab in
                // the world on deploy - people would find their production stopped and no message
                // saying so, which is the worst version of a feature about stopping production.
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WeedLabAutoSell",
                table: "Hideouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WeedLabRunning",
                table: "Hideouts",
                type: "boolean",
                nullable: false,
                // On, for every house that already exists. EF writes false here because it reads the
                // column and not the property initialiser, and false would switch off every lab in
                // the world on deploy - people would find their production stopped and no message
                // saying so, which is the worst version of a feature about stopping production.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CokeLabAutoSell",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "CokeLabRunning",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "WeedLabAutoSell",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "WeedLabRunning",
                table: "Hideouts");
        }
    }
}
