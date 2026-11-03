using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PrimaryAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    PrimaryMobile = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    PrimaryEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Occupation = table.Column<string>(type: "TEXT", nullable: true),
                    TaxNumber = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                    TaxOffice = table.Column<string>(type: "TEXT", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BirthPlace = table.Column<string>(type: "TEXT", nullable: true),
                    Nationality = table.Column<string>(type: "TEXT", nullable: true),
                    FatherName = table.Column<string>(type: "TEXT", nullable: true),
                    MotherName = table.Column<string>(type: "TEXT", nullable: true),
                    HasPersonalInsurance = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReferral = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReferredByCustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PartnerName = table.Column<string>(type: "TEXT", nullable: true),
                    BelongsToOffice = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDriver = table.Column<bool>(type: "INTEGER", nullable: false),
                    LicenseStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LicenseEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LicenseCategory = table.Column<string>(type: "TEXT", nullable: true),
                    LicenseNumber = table.Column<string>(type: "TEXT", nullable: true),
                    HLDiploma = table.Column<string>(type: "TEXT", nullable: true),
                    HasDiscountDocument = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiscountDocumentDescription = table.Column<string>(type: "TEXT", nullable: true),
                    IdentityType = table.Column<string>(type: "TEXT", nullable: true),
                    IdentityNumber = table.Column<string>(type: "TEXT", nullable: true),
                    SecondPhone = table.Column<string>(type: "TEXT", nullable: true),
                    ThirdPhone = table.Column<string>(type: "TEXT", nullable: true),
                    SecondEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ThirdEmail = table.Column<string>(type: "TEXT", nullable: true),
                    BusinessEmail = table.Column<string>(type: "TEXT", nullable: true),
                    BusinessPhone = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Customers_ReferredByCustomerId",
                        column: x => x.ReferredByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EffectiveUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsHistorical = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDetails_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDetails_CustomerId_Category",
                table: "CustomerDetails",
                columns: new[] { "CustomerId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LastName_FirstName",
                table: "Customers",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ReferredByCustomerId",
                table: "Customers",
                column: "ReferredByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TaxNumber",
                table: "Customers",
                column: "TaxNumber",
                unique: true,
                filter: "\"TaxNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransactions_CustomerId_OccurredAtUtc",
                table: "CustomerTransactions",
                columns: new[] { "CustomerId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDetails");

            migrationBuilder.DropTable(
                name: "CustomerTransactions");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
