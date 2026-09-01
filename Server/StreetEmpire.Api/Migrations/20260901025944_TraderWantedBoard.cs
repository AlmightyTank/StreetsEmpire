using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class TraderWantedBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WantedOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Good = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    ShopPricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    Rep = table.Column<int>(type: "integer", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredQuantity = table.Column<int>(type: "integer", nullable: false),
                    ClaimedById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WantedOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WantedOrders_Players_ClaimedById",
                        column: x => x.ClaimedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WantedOrders_Players_FilledById",
                        column: x => x.FilledById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WantedOrders_City_FilledAtUtc_ExpiresAtUtc",
                table: "WantedOrders",
                columns: new[] { "City", "FilledAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WantedOrders_ClaimedById",
                table: "WantedOrders",
                column: "ClaimedById");

            migrationBuilder.CreateIndex(
                name: "IX_WantedOrders_FilledById",
                table: "WantedOrders",
                column: "FilledById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WantedOrders");
        }
    }
}
