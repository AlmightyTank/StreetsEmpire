using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class PasswordResetCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_AccountId_CreatedAtUtc",
                table: "EmailVerifications");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "EmailVerifications",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                // Every code that existed before this column did was a confirmation, and the empty
                // string EF generates here is not a name the enum can be read back from - the rows
                // would fail to load rather than default to anything.
                defaultValue: "ConfirmAddress");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_AccountId_Purpose_CreatedAtUtc",
                table: "EmailVerifications",
                columns: new[] { "AccountId", "Purpose", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_CreatedAtUtc",
                table: "EmailVerifications",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_AccountId_Purpose_CreatedAtUtc",
                table: "EmailVerifications");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_CreatedAtUtc",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "EmailVerifications");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_AccountId_CreatedAtUtc",
                table: "EmailVerifications",
                columns: new[] { "AccountId", "CreatedAtUtc" });
        }
    }
}
