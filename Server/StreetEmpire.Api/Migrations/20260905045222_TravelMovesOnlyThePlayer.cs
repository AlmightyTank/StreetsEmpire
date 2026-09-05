using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class TravelMovesOnlyThePlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Carried_Beer",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Coke",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Carried_CokePurity",
                table: "Players",
                type: "double precision",
                precision: 5,
                scale: 4,
                nullable: false,
                // An empty bag is clean, not worthless. Zero here would have every player's first look
                // at their own pockets report coke at 0% pure, and the first unit added to a pile of
                // nothing would blend against a strength that was never true.
                defaultValue: 1.0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Condoms",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Cut",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Medicine",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Moonshine",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Pistols",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Poison",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Rifles",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Shotguns",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Smgs",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Carried_Weed",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EquippedWeapon",
                table: "Players",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Assignment",
                table: "Pimps",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Everybody on the payroll is at the house until something says otherwise, which is
                // exactly what the model's own default says. An empty string would be a roster full of
                // people whose whereabouts nobody wrote down.
                defaultValue: "hideout");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Pimps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Hideouts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SafeCash",
                table: "Hideouts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Every house that already exists is in the town its owner is standing in, because until
            // this migration ran that was the only town it could have been in - the hideout had no
            // location of its own and was implicitly wherever the player was. Backfilling from the
            // player is not a guess; it is the fact the old schema was expressing by omission.
            migrationBuilder.Sql(@"
                UPDATE ""Hideouts"" AS h
                SET ""City"" = p.""City""
                FROM ""Players"" AS p
                WHERE p.""Id"" = h.""PlayerId"";");

            // A hideout with no owner is not a thing the schema allows, but somewhere real beats the
            // empty string if one ever turns up.
            migrationBuilder.Sql(@"UPDATE ""Hideouts"" SET ""City"" = 'New York' WHERE ""City"" = '';");

            // Existing pimps were all written before assignments existed, so they are all at the house.
            // The column default covers rows inserted from here on; this covers the ones already there.
            migrationBuilder.Sql(@"UPDATE ""Pimps"" SET ""Assignment"" = 'hideout' WHERE ""Assignment"" = '';");

            // Nobody is carrying anything on the day this ships. Every good a player owns is on the
            // shelves at their hideout, which is where it already was - those columns did not move and
            // are not touched here. The bag starts empty and is filled by a deliberate withdrawal,
            // which is the decision this whole change exists to create.
            migrationBuilder.Sql(@"UPDATE ""Players"" SET ""Carried_CokePurity"" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Carried_Beer",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Coke",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_CokePurity",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Condoms",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Cut",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Medicine",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Moonshine",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Pistols",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Poison",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Rifles",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Shotguns",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Smgs",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Carried_Weed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EquippedWeapon",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Assignment",
                table: "Pimps");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Pimps");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Hideouts");

            migrationBuilder.DropColumn(
                name: "SafeCash",
                table: "Hideouts");
        }
    }
}
