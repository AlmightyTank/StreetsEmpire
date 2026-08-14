using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Territories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    City = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    GarrisonThugs = table.Column<int>(type: "integer", nullable: false),
                    HeldSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProtectedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Territories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Territories_Players_HolderId",
                        column: x => x.HolderId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Territories_HolderId",
                table: "Territories",
                column: "HolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Territories_Name",
                table: "Territories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Territories");
        }
    }
}
