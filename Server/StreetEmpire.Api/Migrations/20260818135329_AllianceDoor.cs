using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllianceDoor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added before the old column goes, and converted rather than defaulted. EF scaffolded this
            // as a drop followed by an add defaulting to zero, which is Open - so every crew that had
            // shut its door would have come out of the migration wide open to anybody, which is the one
            // outcome a door setting must never produce on its own.
            migrationBuilder.AddColumn<int>(
                name: "Door",
                table: "Alliances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // A closed door already accepted applications before this existed, so that is what it was:
            // by application, not invitation-only. Nobody's crew changes behaviour across the upgrade.
            migrationBuilder.Sql(
                "UPDATE \"Alliances\" SET \"Door\" = CASE WHEN \"OpenToJoin\" THEN 0 ELSE 1 END;");

            migrationBuilder.DropColumn(
                name: "OpenToJoin",
                table: "Alliances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OpenToJoin",
                table: "Alliances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Three states fold back into two. Anything that was not Open becomes a closed door, which
            // is the most a boolean can carry.
            migrationBuilder.Sql(
                "UPDATE \"Alliances\" SET \"OpenToJoin\" = (\"Door\" = 0);");

            migrationBuilder.DropColumn(
                name: "Door",
                table: "Alliances");
        }
    }
}
