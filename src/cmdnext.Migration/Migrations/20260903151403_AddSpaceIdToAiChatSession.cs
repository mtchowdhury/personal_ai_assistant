using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceIdToAiChatSession : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                schema: "ai",
                table: "AiChatSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiChatSessions_SpaceId",
                schema: "ai",
                table: "AiChatSessions",
                column: "SpaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiChatSessions_SpaceId",
                schema: "ai",
                table: "AiChatSessions");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                schema: "ai",
                table: "AiChatSessions");
        }
    }
}
