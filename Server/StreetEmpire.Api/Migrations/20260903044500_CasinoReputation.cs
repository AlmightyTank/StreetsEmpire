using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StreetEmpire.Api.Data;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GameDbContext))]
    [Migration("20260903044500_CasinoReputation")]
    public partial class CasinoReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CasinoRep",
                table: "Players",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CasinoRep",
                table: "Players");
        }
    }
}
