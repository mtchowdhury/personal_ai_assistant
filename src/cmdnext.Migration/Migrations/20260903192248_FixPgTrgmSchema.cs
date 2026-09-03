using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class FixPgTrgmSchema : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddSpacesModule's `CREATE EXTENSION IF NOT EXISTS pg_trgm` can land in whatever
            // schema is first on search_path at migration time rather than "public" (observed:
            // it landed in "ai" on a fresh v17 restore). The app's search_path is "$user",
            // public, so similarity()/the % operator become invisible and any
            // EF.Functions.TrigramsSimilarity query throws "function similarity(text, text)
            // does not exist". Force it onto public, idempotently, regardless of where it
            // ended up.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_extension e
                        WHERE e.extname = 'pg_trgm' AND e.extnamespace::regnamespace::text <> 'public'
                    ) THEN
                        ALTER EXTENSION pg_trgm SET SCHEMA public;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: moving pg_trgm back to a non-standard schema would just reintroduce the bug.
        }
    }
}
