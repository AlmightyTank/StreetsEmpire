using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <summary>
    /// Comps move from a double of dollars to a whole number of cents.
    ///
    /// Scaffolding this produced a drop followed by an add, which is the same shape as this and would
    /// have quietly emptied the cage: every player's balance replaced by the column default on the
    /// deploy that shipped it, with nothing in the log to say so. The column is added, filled from the
    /// one it replaces, and only then is the old one dropped.
    /// </summary>
    public partial class CompsInCents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CasinoCompsCents",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Rounded to the nearest cent, which is the most the old column can be trusted to for a
            // balance that was accumulated a hundredth of a wager at a time. The cast is written out
            // because ROUND on a double precision returns a double, and a silent assignment cast is
            // not the thing to rely on when the value is somebody's money.
            migrationBuilder.Sql(
                @"UPDATE ""Players"" SET ""CasinoCompsCents"" = ROUND(""CasinoComps"" * 100)::bigint;");

            migrationBuilder.DropColumn(
                name: "CasinoComps",
                table: "Players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CasinoComps",
                table: "Players",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.Sql(
                @"UPDATE ""Players"" SET ""CasinoComps"" = ""CasinoCompsCents"" / 100.0;");

            migrationBuilder.DropColumn(
                name: "CasinoCompsCents",
                table: "Players");
        }
    }
}
