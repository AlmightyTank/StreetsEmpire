using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class CasinoFreeSpins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CasinoFreeSpinBet",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "CasinoFreeSpinLanes",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CasinoFreeSpinMachine",
                table: "Players",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CasinoFreeSpins",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreeSpin",
                table: "CasinoTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CasinoFreeSpinBet",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CasinoFreeSpinLanes",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CasinoFreeSpinMachine",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CasinoFreeSpins",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsFreeSpin",
                table: "CasinoTransactions");
        }
    }
}
