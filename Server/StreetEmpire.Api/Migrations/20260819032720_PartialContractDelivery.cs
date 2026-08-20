using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class PartialContractDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaimedById",
                table: "Contracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveredQuantity",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Orders filled before delivery was tracked did have all of their goods handed over - the
            // old rule accepted nothing less. Left at the column default they would read as untouched,
            // which would be a record of something that never happened.
            migrationBuilder.Sql("""
                UPDATE "Contracts"
                SET "DeliveredQuantity" = "Quantity", "ClaimedById" = "FilledById"
                WHERE "FilledAtUtc" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClaimedById",
                table: "Contracts",
                column: "ClaimedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Players_ClaimedById",
                table: "Contracts",
                column: "ClaimedById",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Players_ClaimedById",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ClaimedById",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClaimedById",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DeliveredQuantity",
                table: "Contracts");
        }
    }
}
