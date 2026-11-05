using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfficeOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GrossPremium = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    NetPremium = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    OfficeCommission = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    OverCommission = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfficeOperations_BusinessRecords_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "BusinessRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeOperations_BusinessRecords_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "BusinessRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeOperations_CompanyId",
                table: "OfficeOperations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeOperations_Kind_OccurredOn",
                table: "OfficeOperations",
                columns: new[] { "Kind", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeOperations_PartnerId",
                table: "OfficeOperations",
                column: "PartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficeOperations");
        }
    }
}
