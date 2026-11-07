using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HierarchicalSystemCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FilterOptions_Category_Value",
                table: "FilterOptions");

            migrationBuilder.AddColumn<string>(
                name: "BranchScope",
                table: "FilterOptions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyScope",
                table: "FilterOptions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_FilterOptions_Category_Value_CompanyScope_BranchScope",
                table: "FilterOptions",
                columns: new[] { "Category", "Value", "CompanyScope", "BranchScope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FilterOptions_Category_Value_CompanyScope_BranchScope",
                table: "FilterOptions");

            migrationBuilder.DropColumn(
                name: "BranchScope",
                table: "FilterOptions");

            migrationBuilder.DropColumn(
                name: "CompanyScope",
                table: "FilterOptions");

            migrationBuilder.CreateIndex(
                name: "IX_FilterOptions_Category_Value",
                table: "FilterOptions",
                columns: new[] { "Category", "Value" },
                unique: true);
        }
    }
}
