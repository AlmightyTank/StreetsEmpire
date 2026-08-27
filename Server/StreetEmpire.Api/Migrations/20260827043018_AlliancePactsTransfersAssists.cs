using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AlliancePactsTransfersAssists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllianceAssistCalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CombatMissionId = table.Column<long>(type: "bigint", nullable: false),
                    DefenderAllianceId = table.Column<long>(type: "bigint", nullable: false),
                    AllyAllianceId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ThugsSent = table.Column<int>(type: "integer", nullable: false),
                    PistolsSent = table.Column<int>(type: "integer", nullable: false),
                    ShotgunsSent = table.Column<int>(type: "integer", nullable: false),
                    SmgsSent = table.Column<int>(type: "integer", nullable: false),
                    RiflesSent = table.Column<int>(type: "integer", nullable: false),
                    RespondedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceAssistCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllianceAssistCalls_Alliances_AllyAllianceId",
                        column: x => x.AllyAllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceAssistCalls_Alliances_DefenderAllianceId",
                        column: x => x.DefenderAllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceAssistCalls_CombatMissions_CombatMissionId",
                        column: x => x.CombatMissionId,
                        principalTable: "CombatMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceAssistCalls_Players_RespondedById",
                        column: x => x.RespondedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AlliancePacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestingAllianceId = table.Column<long>(type: "bigint", nullable: false),
                    TargetAllianceId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AnsweredById = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlliancePacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlliancePacts_Alliances_RequestingAllianceId",
                        column: x => x.RequestingAllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlliancePacts_Alliances_TargetAllianceId",
                        column: x => x.TargetAllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlliancePacts_Players_AnsweredById",
                        column: x => x.AnsweredById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AlliancePacts_Players_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AllianceTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllianceId = table.Column<long>(type: "bigint", nullable: false),
                    FromPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Item = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllianceTransfers_Alliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceTransfers_Players_FromPlayerId",
                        column: x => x.FromPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllianceTransfers_Players_ToPlayerId",
                        column: x => x.ToPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceAssistCalls_AllyAllianceId_Status",
                table: "AllianceAssistCalls",
                columns: new[] { "AllyAllianceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceAssistCalls_CombatMissionId_AllyAllianceId",
                table: "AllianceAssistCalls",
                columns: new[] { "CombatMissionId", "AllyAllianceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllianceAssistCalls_DefenderAllianceId_Status",
                table: "AllianceAssistCalls",
                columns: new[] { "DefenderAllianceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceAssistCalls_RespondedById",
                table: "AllianceAssistCalls",
                column: "RespondedById");

            migrationBuilder.CreateIndex(
                name: "IX_AlliancePacts_AnsweredById",
                table: "AlliancePacts",
                column: "AnsweredById");

            migrationBuilder.CreateIndex(
                name: "IX_AlliancePacts_RequestedById",
                table: "AlliancePacts",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_AlliancePacts_RequestingAllianceId_TargetAllianceId_Status",
                table: "AlliancePacts",
                columns: new[] { "RequestingAllianceId", "TargetAllianceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AlliancePacts_TargetAllianceId_Status",
                table: "AlliancePacts",
                columns: new[] { "TargetAllianceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransfers_AllianceId_CreatedAtUtc",
                table: "AllianceTransfers",
                columns: new[] { "AllianceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransfers_FromPlayerId",
                table: "AllianceTransfers",
                column: "FromPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransfers_ToPlayerId",
                table: "AllianceTransfers",
                column: "ToPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllianceAssistCalls");

            migrationBuilder.DropTable(
                name: "AlliancePacts");

            migrationBuilder.DropTable(
                name: "AllianceTransfers");
        }
    }
}
