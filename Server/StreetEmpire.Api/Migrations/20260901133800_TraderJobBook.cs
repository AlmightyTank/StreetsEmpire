using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class TraderJobBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The two boards become one book, and the old rows go with the old tables.
            //
            // Regenerating rather than carrying across, deliberately. Both tables held short-lived
            // generated work with a deadline on it - the longest-lived row is fourteen hours old - so
            // what is lost is one evening of a board that refills itself, against a column-by-column
            // migration of two shapes into a third that would have to be right first time and could
            // never be tested against anything but its own author's guess at the data.
            //
            // Part-delivered jobs go with them, which is the one real cost: goods handed over and not
            // yet paid their premium. Small, bounded, and the alternative is worse.
            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "WantedOrders");

            migrationBuilder.AddColumn<DateTime>(
                name: "JobRerollsResetAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JobRerollsUsed",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TraderJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Buyer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Good = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    ReferencePricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    MinimumPurityPercent = table.Column<int>(type: "integer", nullable: true),
                    Rep = table.Column<int>(type: "integer", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredQuantity = table.Column<int>(type: "integer", nullable: false),
                    ClaimedById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraderJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraderJobs_Players_ClaimedById",
                        column: x => x.ClaimedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TraderJobs_Players_FilledById",
                        column: x => x.FilledById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TraderJobLeads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    DealtAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraderJobLeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraderJobLeads_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TraderJobLeads_TraderJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TraderJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TraderJobLeads_JobId",
                table: "TraderJobLeads",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_TraderJobLeads_PlayerId_City_Slot",
                table: "TraderJobLeads",
                columns: new[] { "PlayerId", "City", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TraderJobs_City_FilledAtUtc_ExpiresAtUtc",
                table: "TraderJobs",
                columns: new[] { "City", "FilledAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TraderJobs_ClaimedById",
                table: "TraderJobs",
                column: "ClaimedById");

            migrationBuilder.CreateIndex(
                name: "IX_TraderJobs_FilledById",
                table: "TraderJobs",
                column: "FilledById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TraderJobLeads");

            migrationBuilder.DropTable(
                name: "TraderJobs");

            migrationBuilder.DropColumn(
                name: "JobRerollsResetAtUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "JobRerollsUsed",
                table: "Players");

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimedById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledById = table.Column<Guid>(type: "uuid", nullable: true),
                    Buyer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeliveredQuantity = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Good = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ListPricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    MinimumPurityPercent = table.Column<int>(type: "integer", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Players_ClaimedById",
                        column: x => x.ClaimedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Contracts_Players_FilledById",
                        column: x => x.FilledById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WantedOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimedById = table.Column<Guid>(type: "uuid", nullable: true),
                    FilledById = table.Column<Guid>(type: "uuid", nullable: true),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeliveredQuantity = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Good = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PricePerUnit = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Rep = table.Column<int>(type: "integer", nullable: false),
                    ShopPricePerUnit = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WantedOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WantedOrders_Players_ClaimedById",
                        column: x => x.ClaimedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WantedOrders_Players_FilledById",
                        column: x => x.FilledById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_City_FilledAtUtc_ExpiresAtUtc",
                table: "Contracts",
                columns: new[] { "City", "FilledAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClaimedById",
                table: "Contracts",
                column: "ClaimedById");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_FilledById",
                table: "Contracts",
                column: "FilledById");

            migrationBuilder.CreateIndex(
                name: "IX_WantedOrders_City_FilledAtUtc_ExpiresAtUtc",
                table: "WantedOrders",
                columns: new[] { "City", "FilledAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WantedOrders_ClaimedById",
                table: "WantedOrders",
                column: "ClaimedById");

            migrationBuilder.CreateIndex(
                name: "IX_WantedOrders_FilledById",
                table: "WantedOrders",
                column: "FilledById");
        }
    }
}
