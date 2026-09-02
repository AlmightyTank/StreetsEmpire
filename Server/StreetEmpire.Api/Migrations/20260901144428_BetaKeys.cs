using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class BetaKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BetaKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssuedToAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    Uses = table.Column<int>(type: "integer", nullable: false),
                    RedeemedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetaKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BetaKeys_Accounts_IssuedToAccountId",
                        column: x => x.IssuedToAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BetaKeys_Accounts_RedeemedByAccountId",
                        column: x => x.RedeemedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.Sql("""
                WITH alphabet AS (
                    SELECT '23456789ABCDEFGHJKMNPQRSTVWXYZ'::text AS chars
                ),
                eligible AS (
                    SELECT
                        "Id",
                        row_number() OVER (ORDER BY "CreatedAtUtc", "Id") AS rn
                    FROM "Accounts"
                    WHERE NOT "IsBot"
                ),
                generated AS (
                    SELECT
                        eligible."Id",
                        'SE' || string_agg(
                            substr(
                                alphabet.chars,
                                (get_byte(decode(md5(eligible."Id"::text || ':' || eligible.rn::text), 'hex'), slot - 1) % length(alphabet.chars)) + 1,
                                1),
                            '' ORDER BY slot) AS "Code"
                    FROM eligible
                    CROSS JOIN alphabet
                    CROSS JOIN generate_series(1, 10) AS slots(slot)
                    GROUP BY eligible."Id", eligible.rn
                )
                INSERT INTO "BetaKeys" (
                    "Id",
                    "Code",
                    "IssuedToAccountId",
                    "Label",
                    "MaxUses",
                    "Uses",
                    "CreatedAtUtc",
                    "Version")
                SELECT
                    "Id",
                    "Code",
                    "Id",
                    'migration backfill',
                    1,
                    0,
                    NOW(),
                    0
                FROM generated;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BetaKeys_Code",
                table: "BetaKeys",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BetaKeys_IssuedToAccountId",
                table: "BetaKeys",
                column: "IssuedToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BetaKeys_RedeemedByAccountId",
                table: "BetaKeys",
                column: "RedeemedByAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BetaKeys");
        }
    }
}
