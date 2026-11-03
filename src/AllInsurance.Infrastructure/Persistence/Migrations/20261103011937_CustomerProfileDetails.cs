using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerProfileDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerKind",
                table: "Customers",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Φυσικό πρόσωπο");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Customers",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Customers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Customers",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialSecurityNumber",
                table: "Customers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerKind",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SocialSecurityNumber",
                table: "Customers");
        }
    }
}
