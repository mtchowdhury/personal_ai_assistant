using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryChunksForEmbeddings : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryChunks",
                schema: "spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryChunks_Entries_EntryId",
                        column: x => x.EntryId,
                        principalSchema: "spaces",
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryChunks_EntryId",
                schema: "spaces",
                table: "EntryChunks",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryChunks_SpaceId",
                schema: "spaces",
                table: "EntryChunks",
                column: "SpaceId");

            // HNSW index for cosine-similarity ANN search. Built lazily on first use is fine —
            // chunk volume is small at personal scale (hundreds to low thousands of rows).
            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_EntryChunks_Embedding_Hnsw"" ON spaces.""EntryChunks""
                USING hnsw (""Embedding"" vector_cosine_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS spaces.""IX_EntryChunks_Embedding_Hnsw"";");

            migrationBuilder.DropTable(
                name: "EntryChunks",
                schema: "spaces");
        }
    }
}
