using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class Conversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConversationId",
                table: "ChatMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Players_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ConversationMembers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadMessageId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMembers_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMembers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_Id",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_ConversationId_PlayerId",
                table: "ConversationMembers",
                columns: new[] { "ConversationId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_PlayerId",
                table: "ConversationMembers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CreatedById",
                table: "Conversations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastMessageAtUtc",
                table: "Conversations",
                column: "LastMessageAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Conversations_ConversationId",
                table: "ChatMessages",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Every pair that has ever exchanged a direct message becomes a conversation, and their
            // messages are pointed at it, before the columns that recorded the pair are dropped.
            //
            // The scaffold put those drops first, which would have thrown the conversations away and
            // then built somewhere to put them. On this database that would have cost nothing, since
            // the table is empty - which is exactly the kind of luck that hides a migration nobody can
            // run twice.
            //
            // LEAST and GREATEST normalise the pair so a conversation is found the same way whichever
            // of the two happened to write first.
            migrationBuilder.Sql("""
                DO $$
                DECLARE r RECORD; conv BIGINT;
                BEGIN
                    FOR r IN
                        SELECT LEAST("AuthorId", "RecipientId") AS a,
                               GREATEST("AuthorId", "RecipientId") AS b,
                               MIN("CreatedAtUtc") AS first_at,
                               MAX("CreatedAtUtc") AS last_at
                        FROM "ChatMessages"
                        WHERE "Channel" = 3 AND "AuthorId" IS NOT NULL AND "RecipientId" IS NOT NULL
                        GROUP BY 1, 2
                    LOOP
                        INSERT INTO "Conversations" ("IsGroup", "CreatedAtUtc", "LastMessageAtUtc")
                        VALUES (false, r.first_at, r.last_at)
                        RETURNING "Id" INTO conv;

                        INSERT INTO "ConversationMembers"
                            ("ConversationId", "PlayerId", "LastReadMessageId", "JoinedAtUtc")
                        VALUES (conv, r.a, 0, r.first_at), (conv, r.b, 0, r.first_at);

                        UPDATE "ChatMessages"
                        SET "ConversationId" = conv
                        WHERE "Channel" = 3
                          AND LEAST("AuthorId", "RecipientId") = r.a
                          AND GREATEST("AuthorId", "RecipientId") = r.b;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Players_RecipientId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_Channel_AuthorId_RecipientId_Id",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_Channel_RecipientId_Id",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_RecipientId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "ChatMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Conversations_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ConversationMembers");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId_Id",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "ChatMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "ChatMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Channel_AuthorId_RecipientId_Id",
                table: "ChatMessages",
                columns: new[] { "Channel", "AuthorId", "RecipientId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Channel_RecipientId_Id",
                table: "ChatMessages",
                columns: new[] { "Channel", "RecipientId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_RecipientId",
                table: "ChatMessages",
                column: "RecipientId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Players_RecipientId",
                table: "ChatMessages",
                column: "RecipientId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
