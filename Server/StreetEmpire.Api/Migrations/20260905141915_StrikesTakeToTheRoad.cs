using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class StrikesTakeToTheRoad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingStrikes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttackerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OriginCity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetCity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TravelTurns = table.Column<int>(type: "integer", nullable: false),
                    LaunchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArrivesAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TurnsSpent = table.Column<int>(type: "integer", nullable: false),
                    Fare = table.Column<long>(type: "bigint", nullable: false),
                    CommittedRides = table.Column<int>(type: "integer", nullable: false),
                    CommittedPoison = table.Column<int>(type: "integer", nullable: false),
                    CommittedCoke = table.Column<int>(type: "integer", nullable: false),
                    CommittedCokePurity = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    ReturningRides = table.Column<int>(type: "integer", nullable: false),
                    ReturningPoison = table.Column<int>(type: "integer", nullable: false),
                    ReturningCoke = table.Column<int>(type: "integer", nullable: false),
                    ReturningCokePurity = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    ReturningHoes = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingStrikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingStrikes_Players_AttackerId",
                        column: x => x.AttackerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingStrikes_Players_DefenderId",
                        column: x => x.DefenderId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingStrikes_AttackerId",
                table: "PendingStrikes",
                column: "AttackerId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingStrikes_DefenderId_Status",
                table: "PendingStrikes",
                columns: new[] { "DefenderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingStrikes_Status_ArrivesAtUtc",
                table: "PendingStrikes",
                columns: new[] { "Status", "ArrivesAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingStrikes_Status_ReturnsAtUtc",
                table: "PendingStrikes",
                columns: new[] { "Status", "ReturnsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingStrikes");
        }
    }
}
