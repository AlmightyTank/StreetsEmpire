using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class MuleRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntelligenceLevel",
                table: "Hideouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MuleRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginCity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DestinationCity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Good = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    PimpId = table.Column<long>(type: "bigint", nullable: true),
                    PimpName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PimpLoyaltyAtLaunch = table.Column<double>(type: "double precision", nullable: false),
                    AssignedHoes = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CashSent = table.Column<long>(type: "bigint", nullable: false),
                    TravelCost = table.Column<long>(type: "bigint", nullable: false),
                    UpkeepCost = table.Column<long>(type: "bigint", nullable: false),
                    TurnsSpent = table.Column<int>(type: "integer", nullable: false),
                    UnitsBought = table.Column<int>(type: "integer", nullable: false),
                    UnitPricePaid = table.Column<long>(type: "bigint", nullable: false),
                    CashReturned = table.Column<long>(type: "bigint", nullable: false),
                    SeizedUnits = table.Column<int>(type: "integer", nullable: false),
                    HeatAdded = table.Column<double>(type: "double precision", nullable: false),
                    PimpLost = table.Column<bool>(type: "boolean", nullable: false),
                    HoesLost = table.Column<int>(type: "integer", nullable: false),
                    BustChancePercent = table.Column<int>(type: "integer", nullable: false),
                    DefectChancePercent = table.Column<int>(type: "integer", nullable: false),
                    DepartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArrivesAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuleRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuleRuns_Pimps_PimpId",
                        column: x => x.PimpId,
                        principalTable: "Pimps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MuleRuns_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuleRuns_PimpId",
                table: "MuleRuns",
                column: "PimpId");

            migrationBuilder.CreateIndex(
                name: "IX_MuleRuns_PlayerId_SettledAtUtc",
                table: "MuleRuns",
                columns: new[] { "PlayerId", "SettledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleRuns_ReturnsAtUtc",
                table: "MuleRuns",
                column: "ReturnsAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuleRuns");

            migrationBuilder.DropColumn(
                name: "IntelligenceLevel",
                table: "Hideouts");
        }
    }
}
