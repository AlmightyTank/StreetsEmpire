using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AssistRecall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PistolsReturned",
                table: "AllianceAssistCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecalledAtUtc",
                table: "AllianceAssistCalls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiflesReturned",
                table: "AllianceAssistCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShotgunsReturned",
                table: "AllianceAssistCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SmgsReturned",
                table: "AllianceAssistCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ThugsReturned",
                table: "AllianceAssistCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PistolsReturned",
                table: "AllianceAssistCalls");

            migrationBuilder.DropColumn(
                name: "RecalledAtUtc",
                table: "AllianceAssistCalls");

            migrationBuilder.DropColumn(
                name: "RiflesReturned",
                table: "AllianceAssistCalls");

            migrationBuilder.DropColumn(
                name: "ShotgunsReturned",
                table: "AllianceAssistCalls");

            migrationBuilder.DropColumn(
                name: "SmgsReturned",
                table: "AllianceAssistCalls");

            migrationBuilder.DropColumn(
                name: "ThugsReturned",
                table: "AllianceAssistCalls");
        }
    }
}
