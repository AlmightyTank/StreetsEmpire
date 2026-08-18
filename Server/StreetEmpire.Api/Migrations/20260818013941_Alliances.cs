using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class Alliances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllianceDefenders",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "AllianceId",
                table: "Players",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AllianceJoinedAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Alliances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Motto = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    FounderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DuesPercent = table.Column<int>(type: "integer", nullable: false),
                    Treasury = table.Column<long>(type: "bigint", nullable: false),
                    OffensiveThugs = table.Column<int>(type: "integer", nullable: false),
                    DefensiveThugs = table.Column<int>(type: "integer", nullable: false),
                    OpenToJoin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alliances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_AllianceId",
                table: "Players",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_Alliances_Name",
                table: "Alliances",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Alliances_AllianceId",
                table: "Players",
                column: "AllianceId",
                principalTable: "Alliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Alliances_AllianceId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "Alliances");

            migrationBuilder.DropIndex(
                name: "IX_Players_AllianceId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AllianceDefenders",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AllianceId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AllianceJoinedAtUtc",
                table: "Players");
        }
    }
}
