using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerTransactionPaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "CustomerTransactions",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "CustomerTransactions",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "Μετρητά");

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "CustomerTransactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "CustomerTransactions");
        }
    }
}
