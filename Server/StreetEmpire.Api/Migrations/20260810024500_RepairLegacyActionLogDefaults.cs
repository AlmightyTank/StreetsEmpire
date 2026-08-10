using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StreetEmpire.Api.Data;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    [DbContext(typeof(GameDbContext))]
    [Migration("20260810024500_RepairLegacyActionLogDefaults")]
    public partial class RepairLegacyActionLogDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    legacy_column text;
                BEGIN
                    FOREACH legacy_column IN ARRAY ARRAY[
                        'WorkersDelta',
                        'EnforcersDelta',
                        'SuppliesDelta',
                        'MoraleDelta'
                    ]
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_name = 'ActionLogs'
                              AND column_name = legacy_column
                        ) THEN
                            EXECUTE format('UPDATE "ActionLogs" SET %I = 0 WHERE %I IS NULL', legacy_column, legacy_column);
                            EXECUTE format('ALTER TABLE "ActionLogs" ALTER COLUMN %I SET DEFAULT 0', legacy_column);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-preserving repair migration. Leave legacy defaults in place on rollback.
        }
    }
}
