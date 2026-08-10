using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Cash = table.Column<long>(type: "bigint", nullable: false),
                    BankCash = table.Column<long>(type: "bigint", nullable: false),
                    Turns = table.Column<int>(type: "integer", nullable: false),
                    LastTurnUpdateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Pimps = table.Column<int>(type: "integer", nullable: false),
                    Hoes = table.Column<int>(type: "integer", nullable: false),
                    Thugs = table.Column<int>(type: "integer", nullable: false),
                    HoeCutPercent = table.Column<int>(type: "integer", nullable: false),
                    HoeHappiness = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    ThugHappiness = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    Condoms = table.Column<int>(type: "integer", nullable: false),
                    Beer = table.Column<int>(type: "integer", nullable: false),
                    Weapons = table.Column<int>(type: "integer", nullable: false),
                    Weed = table.Column<int>(type: "integer", nullable: false),
                    Coke = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TurnsSpent = table.Column<int>(type: "integer", nullable: false),
                    CashDelta = table.Column<long>(type: "bigint", nullable: false),
                    BankDelta = table.Column<long>(type: "bigint", nullable: false),
                    PimpsDelta = table.Column<int>(type: "integer", nullable: false),
                    HoesDelta = table.Column<int>(type: "integer", nullable: false),
                    ThugsDelta = table.Column<int>(type: "integer", nullable: false),
                    CondomsDelta = table.Column<int>(type: "integer", nullable: false),
                    BeerDelta = table.Column<int>(type: "integer", nullable: false),
                    WeaponsDelta = table.Column<int>(type: "integer", nullable: false),
                    WeedDelta = table.Column<int>(type: "integer", nullable: false),
                    CokeDelta = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionLogs_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionLogs_PlayerId_CreatedAtUtc",
                table: "ActionLogs",
                columns: new[] { "PlayerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_AccountId",
                table: "Players",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionLogs");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
