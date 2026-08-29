using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class GameAnnouncementDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameAnnouncements_ArchivedAtUtc_PublishedAtUtc",
                table: "GameAnnouncements");

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "GameAnnouncements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_GameAnnouncements_IsDraft_ArchivedAtUtc_PublishedAtUtc",
                table: "GameAnnouncements",
                columns: new[] { "IsDraft", "ArchivedAtUtc", "PublishedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameAnnouncements_IsDraft_ArchivedAtUtc_PublishedAtUtc",
                table: "GameAnnouncements");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "GameAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_GameAnnouncements_ArchivedAtUtc_PublishedAtUtc",
                table: "GameAnnouncements",
                columns: new[] { "ArchivedAtUtc", "PublishedAtUtc" });
        }
    }
}
