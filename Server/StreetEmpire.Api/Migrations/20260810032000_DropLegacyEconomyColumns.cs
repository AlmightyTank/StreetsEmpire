using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StreetEmpire.Api.Data;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    [DbContext(typeof(GameDbContext))]
    [Migration("20260810032000_DropLegacyEconomyColumns")]
    public partial class DropLegacyEconomyColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    legacy_column text;
                BEGIN
                    FOREACH legacy_column IN ARRAY ARRAY[
                        'Workers',
                        'Enforcers',
                        'Supplies',
                        'Morale',
                        'Happiness'
                    ]
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_name = 'Players'
                              AND column_name = legacy_column
                        ) THEN
                            EXECUTE format('ALTER TABLE "Players" DROP COLUMN %I', legacy_column);
                        END IF;
                    END LOOP;

                    FOREACH legacy_column IN ARRAY ARRAY[
                        'WorkersDelta',
                        'EnforcersDelta',
                        'SuppliesDelta',
                        'MoraleDelta',
                        'HappinessDelta'
                    ]
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_name = 'ActionLogs'
                              AND column_name = legacy_column
                        ) THEN
                            EXECUTE format('ALTER TABLE "ActionLogs" DROP COLUMN %I', legacy_column);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Legacy 0.1.0 columns were copied into the 0.1.1+ economy columns before this cleanup.
            // Recreating them would reintroduce insert constraints EF no longer satisfies.
        }
    }
}
