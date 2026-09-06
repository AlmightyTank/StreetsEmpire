using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class InsuranceAndSurrender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InsuranceAnswered",
                table: "BlackjackHands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "InsuranceBet",
                table: "BlackjackHands",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "InsuranceOffered",
                table: "BlackjackHands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "InsurancePayout",
                table: "BlackjackHands",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsuranceAnswered",
                table: "BlackjackHands");

            migrationBuilder.DropColumn(
                name: "InsuranceBet",
                table: "BlackjackHands");

            migrationBuilder.DropColumn(
                name: "InsuranceOffered",
                table: "BlackjackHands");

            migrationBuilder.DropColumn(
                name: "InsurancePayout",
                table: "BlackjackHands");
        }
    }
}
