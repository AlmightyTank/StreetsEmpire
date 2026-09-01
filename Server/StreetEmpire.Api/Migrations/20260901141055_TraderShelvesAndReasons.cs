using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class TraderShelvesAndReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Buyer",
                table: "TraderJobs",
                newName: "OnBehalfOf");

            migrationBuilder.AddColumn<int>(
                name: "Reason",
                table: "TraderJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing rows predate the reason, so they would all read as a shelf gap - and a shelf gap
            // shuts a line at the counter until somebody fills it. Anything already being done for a
            // named place becomes a favour, which is what it was; the dealer's own become gaps, which is
            // what they were - except on the three lines every counter always carries, which can never
            // be a gap and become a promise instead. Nothing is invented, and no town wakes up unable to
            // sell a pistol because of a column that did not exist when the row was written.
            migrationBuilder.Sql("""
                UPDATE "TraderJobs" SET "Reason" = CASE
                    WHEN "OnBehalfOf" IS NOT NULL THEN 1
                    WHEN "Good" IN ('condoms', 'beer', 'pistols') THEN 2
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TraderJobs");

            migrationBuilder.RenameColumn(
                name: "OnBehalfOf",
                table: "TraderJobs",
                newName: "Buyer");
        }
    }
}
