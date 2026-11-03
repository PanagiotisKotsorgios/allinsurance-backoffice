using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommissionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Insurer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    VehicleUse = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Coverage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Package = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OfficePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    PartnerPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_Insurer_IsActive",
                table: "CommissionRules",
                columns: new[] { "Insurer", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionRules");
        }
    }
}
