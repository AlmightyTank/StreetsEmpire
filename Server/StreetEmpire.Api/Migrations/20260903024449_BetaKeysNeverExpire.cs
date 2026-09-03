using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class BetaKeysNeverExpire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A key lasts until it is spent or taken back. The expiry answered a question nobody was
            // asking and created one: a key handed to a friend who took a fortnight to look at it
            // would stop working on its own, with nobody at fault and no way to tell which of the two
            // of them had got it wrong.
            //
            // Safe to drop rather than leave sitting unused - nothing in the world had one set, which
            // is its own answer about whether the feature was wanted.
            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "BetaKeys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "BetaKeys",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
