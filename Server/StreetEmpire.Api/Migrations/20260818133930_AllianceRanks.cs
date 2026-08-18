using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllianceRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllianceRank",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinRankToBorrow",
                table: "Alliances",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MinRankToExpel",
                table: "Alliances",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "MinRankToInvite",
                table: "Alliances",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MinRankToPostDefenders",
                table: "Alliances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinRankToSpendTreasury",
                table: "Alliances",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "AllianceRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllianceId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SentById = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllianceRequests_Alliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "Alliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllianceRequests_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceRequests_AllianceId_Kind",
                table: "AllianceRequests",
                columns: new[] { "AllianceId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceRequests_PlayerId_Kind",
                table: "AllianceRequests",
                columns: new[] { "PlayerId", "Kind" });

            // The thresholds above carry the model defaults rather than a bare zero. Left at zero,
            // every crew that already existed would come out of this migration with no gates at all
            // - every soldier able to spend the treasury and throw people out - which is the exact
            // opposite of what adding a permission system is for.

            // Ranks default to Soldier, which would quietly strip every existing crew of its boss and
            // leave nobody able to set dues, spend the treasury or open the door. Whoever founded a crew
            // was running it before ranks existed, so they are the boss now.
            migrationBuilder.Sql(
                "UPDATE \"Players\" p SET \"AllianceRank\" = 3 " +
                "FROM \"Alliances\" a WHERE p.\"AllianceId\" = a.\"Id\" AND a.\"FounderId\" = p.\"Id\";");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllianceRequests");

            migrationBuilder.DropColumn(
                name: "AllianceRank",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MinRankToBorrow",
                table: "Alliances");

            migrationBuilder.DropColumn(
                name: "MinRankToExpel",
                table: "Alliances");

            migrationBuilder.DropColumn(
                name: "MinRankToInvite",
                table: "Alliances");

            migrationBuilder.DropColumn(
                name: "MinRankToPostDefenders",
                table: "Alliances");

            migrationBuilder.DropColumn(
                name: "MinRankToSpendTreasury",
                table: "Alliances");
        }
    }
}
