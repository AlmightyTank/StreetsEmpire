using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class BlackjackSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The new columns go on first, because the round that already exists has to be carried
            // into them before the columns it lives in are taken away.
            migrationBuilder.AddColumn<int>(
                name: "ActiveHand",
                table: "BlackjackHands",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HandsJson",
                table: "BlackjackHands",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Splits",
                table: "BlackjackHands",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // A round used to be one hand held in its own columns. It is a list now, so every existing
            // row becomes a list of exactly one - which is what it always was - rather than losing the
            // cards it was played with and showing a blank line in the ledger for ever.
            migrationBuilder.Sql(
                """
                UPDATE "BlackjackHands"
                SET "HandsJson" =
                    '[{"Cards":' || "PlayerCardsJson" ||
                    ',"Bet":' || "Bet" ||
                    ',"Doubled":' || (CASE WHEN "Doubled" THEN 'true' ELSE 'false' END) ||
                    ',"Status":"' || "Status" ||
                    '","Payout":' || "Payout" || '}]'
                WHERE "PlayerCardsJson" IS NOT NULL AND "PlayerCardsJson" <> '';
                """);

            migrationBuilder.DropColumn(
                name: "Doubled",
                table: "BlackjackHands");

            migrationBuilder.DropColumn(
                name: "PlayerCardsJson",
                table: "BlackjackHands");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveHand",
                table: "BlackjackHands");

            migrationBuilder.DropColumn(
                name: "HandsJson",
                table: "BlackjackHands");

            migrationBuilder.DropColumn(
                name: "Splits",
                table: "BlackjackHands");

            migrationBuilder.AddColumn<bool>(
                name: "Doubled",
                table: "BlackjackHands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlayerCardsJson",
                table: "BlackjackHands",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");
        }
    }
}
