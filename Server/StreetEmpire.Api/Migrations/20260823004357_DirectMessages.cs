using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    public partial class DirectMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
