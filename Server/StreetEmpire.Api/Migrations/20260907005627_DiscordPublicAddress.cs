using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class DiscordPublicAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordPublicUrl",
                table: "GameSettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "DiscordPublicUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordPublicUrl",
                table: "GameSettings");
        }
    }
}
