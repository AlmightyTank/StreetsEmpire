using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using StreetEmpire.Api.Data;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GameDbContext))]
    [Migration("20260903043000_AddCasinoTransactions")]
    public partial class AddCasinoTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CasinoTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MachineKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BetAmount = table.Column<long>(type: "bigint", nullable: false),
                    PayoutAmount = table.Column<long>(type: "bigint", nullable: false),
                    NetResult = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasinoTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasinoTransactions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CasinoTransactions_GameType_MachineKey_CreatedAtUtc",
                table: "CasinoTransactions",
                columns: new[] { "GameType", "MachineKey", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CasinoTransactions_PlayerId_CreatedAtUtc",
                table: "CasinoTransactions",
                columns: new[] { "PlayerId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CasinoTransactions");
        }
    }
}
