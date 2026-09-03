using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class WalkthroughSeen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WalkthroughSeenAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            // Everybody who is already here has already started, and null means "has not seen it" - so
            // without this line the first thing every established player meets after the deploy is a
            // beginner's tour of a game they have been playing for a month. The walkthrough runs after
            // an account is made, and backfilling the world that predates the column is what keeps that
            // sentence true. They can still ask for it from account settings.
            migrationBuilder.Sql(
                @"UPDATE ""Players"" SET ""WalkthroughSeenAtUtc"" = NOW() WHERE ""WalkthroughSeenAtUtc"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WalkthroughSeenAtUtc",
                table: "Players");
        }
    }
}
