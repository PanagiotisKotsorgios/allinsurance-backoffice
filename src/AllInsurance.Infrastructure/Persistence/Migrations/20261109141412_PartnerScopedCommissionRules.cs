using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnerScopedCommissionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PartnerCategory",
                table: "CommissionRules",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerId",
                table: "CommissionRules",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PartnerCategory",
                table: "CommissionRules");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "CommissionRules");
        }
    }
}
