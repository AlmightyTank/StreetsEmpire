using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class CasinoProgressiveJackpot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "JackpotAmount",
                table: "CasinoTransactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "CasinoJackpotDrops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MachineKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    CasinoTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    WonAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasinoJackpotDrops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasinoJackpotDrops_CasinoTransactions_CasinoTransactionId",
                        column: x => x.CasinoTransactionId,
                        principalTable: "CasinoTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CasinoJackpotDrops_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CasinoJackpotDrops_CasinoTransactionId",
                table: "CasinoJackpotDrops",
                column: "CasinoTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CasinoJackpotDrops_MachineKey_WonAtUtc",
                table: "CasinoJackpotDrops",
                columns: new[] { "MachineKey", "WonAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CasinoJackpotDrops_PlayerId",
                table: "CasinoJackpotDrops",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CasinoJackpotDrops");

            migrationBuilder.DropColumn(
                name: "JackpotAmount",
                table: "CasinoTransactions");
        }
    }
}
