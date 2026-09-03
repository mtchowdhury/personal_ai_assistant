using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddSpacesModule : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pg_trgm powers fuzzy/typo-tolerant search (TrigramsSimilarity), used as a fallback
            // when full-text search finds nothing.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.EnsureSchema(
                name: "spaces");

            migrationBuilder.CreateTable(
                name: "Spaces",
                schema: "spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Conventions = table.Column<string>(type: "text", nullable: true),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false),
                    SchemaJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                schema: "spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nodes_Nodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "spaces",
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Nodes_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "spaces",
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                schema: "spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    DeletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entries_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "spaces",
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Entries_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "spaces",
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                schema: "spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageProvider = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Entries_EntryId",
                        column: x => x.EntryId,
                        principalSchema: "spaces",
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attachments_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "spaces",
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attachments_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "spaces",
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_EntryId",
                schema: "spaces",
                table: "Attachments",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_NodeId",
                schema: "spaces",
                table: "Attachments",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_SpaceId",
                schema: "spaces",
                table: "Attachments",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_NodeId",
                schema: "spaces",
                table: "Entries",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_SpaceId_Type_OccurredOn",
                schema: "spaces",
                table: "Entries",
                columns: new[] { "SpaceId", "Type", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_Tags",
                schema: "spaces",
                table: "Entries",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_UserId",
                schema: "spaces",
                table: "Entries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ParentId",
                schema: "spaces",
                table: "Nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_SpaceId_Path",
                schema: "spaces",
                table: "Nodes",
                columns: new[] { "SpaceId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_UserId",
                schema: "spaces",
                table: "Spaces",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_UserId_Slug",
                schema: "spaces",
                table: "Spaces",
                columns: new[] { "UserId", "Slug" },
                unique: true);

            // Functional GIN index on the exact expression SpaceService.SearchAsync uses
            // (EF.Functions.ToTsVector("simple", e.Title + " " + e.Body)) so full-text search
            // is index-backed without needing a separately-maintained generated column that
            // could drift from the query shape. "simple" config (no stemming) suits mixed
            // German/English content.
            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_Entries_SearchVector"" ON spaces.""Entries""
                USING gin (to_tsvector('simple', ""Title"" || ' ' || ""Body""));
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_Entries_Title_Trgm"" ON spaces.""Entries"" USING gin (""Title"" gin_trgm_ops);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_Entries_Body_Trgm"" ON spaces.""Entries"" USING gin (""Body"" gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS spaces.""IX_Entries_SearchVector"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS spaces.""IX_Entries_Title_Trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS spaces.""IX_Entries_Body_Trgm"";");

            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "spaces");

            migrationBuilder.DropTable(
                name: "Entries",
                schema: "spaces");

            migrationBuilder.DropTable(
                name: "Nodes",
                schema: "spaces");

            migrationBuilder.DropTable(
                name: "Spaces",
                schema: "spaces");
        }
    }
}
