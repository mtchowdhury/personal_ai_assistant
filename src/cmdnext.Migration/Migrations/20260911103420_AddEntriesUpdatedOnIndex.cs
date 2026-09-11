using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddEntriesUpdatedOnIndex : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Supports the "updated" sort in GetEntriesAsync (last-modified first), which a
            // reference space like the imported note archive uses as its default order. Without
            // it, listing a space with thousands of entries sorts the whole filtered set on
            // every load. The COALESCE mirrors the ORDER BY expression exactly so the index is
            // usable, and DESC matches the query's direction.
            //
            // Raw SQL because the index is over an expression EF cannot model.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Entries_SpaceId_UpdatedOn""
                ON spaces.""Entries"" (""SpaceId"", (COALESCE(""UpdatedOn"", ""CreatedOn"")) DESC);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS spaces.""IX_Entries_SpaceId_UpdatedOn"";");
        }
    }
}
