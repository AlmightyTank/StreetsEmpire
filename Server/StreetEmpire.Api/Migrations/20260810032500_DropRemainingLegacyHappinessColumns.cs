using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StreetEmpire.Api.Data;

#nullable disable

namespace StreetEmpire.Api.Migrations
{
    [DbContext(typeof(GameDbContext))]
    [Migration("20260810032500_DropRemainingLegacyHappinessColumns")]
    public partial class DropRemainingLegacyHappinessColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    legacy_column text;
                BEGIN
                    FOREACH legacy_column IN ARRAY ARRAY[
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
            // Legacy happiness values were copied into HoeHappiness and ThugHappiness before cleanup.
            // Recreating the old columns would reintroduce insert constraints EF no longer satisfies.
        }
    }
}
