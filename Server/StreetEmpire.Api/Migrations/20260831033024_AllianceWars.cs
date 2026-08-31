using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllianceWars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllianceWars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeclaringAllianceId = table.Column<long>(type: "bigint", nullable: false),
                    TargetAllianceId = table.Column<long>(type: "bigint", nullable: false),
                    DeclaredById = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Stake = table.Column<long>(type: "bigint", nullable: false),
                    DeclaringScore = table.Column<int>(type: "integer", nullable: false),
                    TargetScore = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WinnerAllianceId = table.Column<long>(type: "bigint", nullable: true),
                    Tribute = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceWars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllianceWars_Alliances_DeclaringAllianceId",
                        column: x => x.DeclaringAllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceWars_Alliances_TargetAllianceId",
                        column: x => x.TargetAllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceWars_Players_DeclaredById",
                        column: x => x.DeclaredById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceWars_DeclaredById",
                table: "AllianceWars",
                column: "DeclaredById");

            migrationBuilder.CreateIndex(
                name: "IX_AllianceWars_DeclaringAllianceId_Status",
                table: "AllianceWars",
                columns: new[] { "DeclaringAllianceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceWars_Status_EndsAtUtc",
                table: "AllianceWars",
                columns: new[] { "Status", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceWars_TargetAllianceId_Status",
                table: "AllianceWars",
                columns: new[] { "TargetAllianceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllianceWars");
        }
    }
}
