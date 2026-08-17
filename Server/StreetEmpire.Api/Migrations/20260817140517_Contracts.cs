using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class Contracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Buyer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Good = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    ListPricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    MinimumPurityPercent = table.Column<int>(type: "integer", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FilledById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Players_FilledById",
                        column: x => x.FilledById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_City_FilledAtUtc_ExpiresAtUtc",
                table: "Contracts",
                columns: new[] { "City", "FilledAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_FilledById",
                table: "Contracts",
                column: "FilledById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");
        }
    }
}
