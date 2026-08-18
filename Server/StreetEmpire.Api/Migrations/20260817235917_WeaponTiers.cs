using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <summary>
    /// Splits the single weapon column into four tiers.
    ///
    /// The rename is the whole of the data migration and it is deliberately to pistols. EF's own guess
    /// was Smgs - it matches columns by name and Smgs happened to sort first - which would have turned
    /// every gun in the game into a $2,500 submachine gun and inflated the entire field's net worth
    /// fivefold overnight. Every existing weapon is the entry-level gun, because that is what it was:
    /// one generic weapon, worth exactly one armed thug in a fight.
    ///
    /// So nobody's combat strength moves at all. A pistol's firepower is 1, which is precisely what the
    /// old weapon contributed, and a player who never buys anything better fights with the same numbers
    /// they had yesterday. What does move is paper value: the old weapon was priced at $500 and a pistol
    /// is $250, so a rack halves in net worth. That falls on every player and every rival alike, on the
    /// same asset, in the same proportion.
    /// </summary>
    public partial class WeaponTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Weapons",
                table: "Players",
                newName: "Pistols");

            migrationBuilder.AddColumn<int>(
                name: "Shotguns",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Smgs",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rifles",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarriedPistols",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarriedShotguns",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarriedSmgs",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarriedRifles",
                table: "CombatMissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // A raid already in the air is carrying guns it was never asked to itemise. Left at zero it
            // would arrive with no firepower at all and lose a fight it was winning when it set off, so
            // the crew is recorded as carrying what it actually left with: pistols.
            migrationBuilder.Sql(
                "UPDATE \"CombatMissions\" SET \"CarriedPistols\" = \"RemainingWeapons\" WHERE \"Status\" <> 'Complete';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Folds every gun back into one column. Anything above a pistol keeps its count but loses
            // which kind it was, which is the most a single column can carry.
            migrationBuilder.Sql(
                "UPDATE \"Players\" SET \"Pistols\" = \"Pistols\" + \"Shotguns\" + \"Smgs\" + \"Rifles\";");

            migrationBuilder.DropColumn(name: "Shotguns", table: "Players");
            migrationBuilder.DropColumn(name: "Smgs", table: "Players");
            migrationBuilder.DropColumn(name: "Rifles", table: "Players");

            migrationBuilder.DropColumn(name: "CarriedPistols", table: "CombatMissions");
            migrationBuilder.DropColumn(name: "CarriedShotguns", table: "CombatMissions");
            migrationBuilder.DropColumn(name: "CarriedSmgs", table: "CombatMissions");
            migrationBuilder.DropColumn(name: "CarriedRifles", table: "CombatMissions");

            migrationBuilder.RenameColumn(
                name: "Pistols",
                table: "Players",
                newName: "Weapons");
        }
    }
}
