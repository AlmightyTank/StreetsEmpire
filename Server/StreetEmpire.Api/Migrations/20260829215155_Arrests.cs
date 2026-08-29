using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class Arrests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "JailedAtUtc",
                table: "Pimps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Arrests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hoes = table.Column<int>(type: "integer", nullable: false),
                    Thugs = table.Column<int>(type: "integer", nullable: false),
                    PimpId = table.Column<long>(type: "bigint", nullable: true),
                    PimpName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PimpLoyaltyAtArrest = table.Column<double>(type: "double precision", nullable: false),
                    BailAmount = table.Column<long>(type: "bigint", nullable: false),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    District = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HeatAtArrest = table.Column<double>(type: "double precision", nullable: false),
                    ChancePercent = table.Column<int>(type: "integer", nullable: false),
                    ArrestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BailDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Arrests_Pimps_PimpId",
                        column: x => x.PimpId,
                        principalTable: "Pimps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Arrests_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Arrests_BailDeadlineUtc",
                table: "Arrests",
                column: "BailDeadlineUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Arrests_PimpId",
                table: "Arrests",
                column: "PimpId");

            migrationBuilder.CreateIndex(
                name: "IX_Arrests_PlayerId_SettledAtUtc",
                table: "Arrests",
                columns: new[] { "PlayerId", "SettledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arrests");

            migrationBuilder.DropColumn(
                name: "JailedAtUtc",
                table: "Pimps");
        }
    }
}
