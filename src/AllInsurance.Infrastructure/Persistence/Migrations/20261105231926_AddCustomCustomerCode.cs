using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomCustomerCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomCode",
                table: "Customers",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomCode",
                table: "Customers",
                column: "CustomCode",
                unique: true,
                filter: "\"CustomCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomCode",
                table: "Customers");
        }
    }
}
