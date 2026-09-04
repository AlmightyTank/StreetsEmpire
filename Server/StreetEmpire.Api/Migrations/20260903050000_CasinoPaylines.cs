using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StreetEmpire.Api.Data;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GameDbContext))]
    [Migration("20260903050000_CasinoPaylines")]
    public partial class CasinoPaylines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                table: "CasinoTransactions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<int>(
                name: "Paylines",
                table: "CasinoTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "WinningPaylines",
                table: "CasinoTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Paylines",
                table: "CasinoTransactions");

            migrationBuilder.DropColumn(
                name: "WinningPaylines",
                table: "CasinoTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                table: "CasinoTransactions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240);
        }
    }
}
