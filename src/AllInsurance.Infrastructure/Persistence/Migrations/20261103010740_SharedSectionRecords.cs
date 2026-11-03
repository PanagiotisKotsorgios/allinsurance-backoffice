using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedSectionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SectionKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RelatedParty = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessRecords_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRecords_CustomerId",
                table: "BusinessRecords",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRecords_SectionKey_Title",
                table: "BusinessRecords",
                columns: new[] { "SectionKey", "Title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessRecords");
        }
    }
}
