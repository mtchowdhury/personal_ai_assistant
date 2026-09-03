using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryEntity : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Category",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "finance",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "finance",
                table: "ExpenseItems");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                schema: "finance",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                schema: "finance",
                table: "ExpenseItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                schema: "finance",
                table: "Budgets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PartyLabel = table.Column<string>(type: "text", nullable: true),
                    ShowPartyField = table.Column<bool>(type: "boolean", nullable: false),
                    IsProtected = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId",
                schema: "finance",
                table: "Expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseItems_CategoryId",
                schema: "finance",
                table: "ExpenseItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId",
                schema: "finance",
                table: "Budgets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_CategoryId",
                schema: "finance",
                table: "Budgets",
                columns: new[] { "UserId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId",
                schema: "finance",
                table: "Categories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Name",
                schema: "finance",
                table: "Categories",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Budgets_Categories_CategoryId",
                schema: "finance",
                table: "Budgets",
                column: "CategoryId",
                principalSchema: "finance",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseItems_Categories_CategoryId",
                schema: "finance",
                table: "ExpenseItems",
                column: "CategoryId",
                principalSchema: "finance",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Categories_CategoryId",
                schema: "finance",
                table: "Expenses",
                column: "CategoryId",
                principalSchema: "finance",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Budgets_Categories_CategoryId",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseItems_Categories_CategoryId",
                schema: "finance",
                table: "ExpenseItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Categories_CategoryId",
                schema: "finance",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CategoryId",
                schema: "finance",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseItems_CategoryId",
                schema: "finance",
                table: "ExpenseItems");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_CategoryId",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_CategoryId",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "finance",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "finance",
                table: "ExpenseItems");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "finance",
                table: "Expenses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "finance",
                table: "ExpenseItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "finance",
                table: "Budgets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Category",
                schema: "finance",
                table: "Budgets",
                columns: new[] { "UserId", "Category" },
                unique: true);
        }
    }
}
