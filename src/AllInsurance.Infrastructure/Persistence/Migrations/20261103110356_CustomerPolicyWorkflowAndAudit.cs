using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllInsurance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPolicyWorkflowAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessRecordId",
                table: "CustomerTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "BusinessRecords",
                type: "TEXT",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "BusinessRecords",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ActorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerActivities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransactions_BusinessRecordId",
                table: "CustomerTransactions",
                column: "BusinessRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerActivities_CustomerId_OccurredAtUtc",
                table: "CustomerActivities",
                columns: new[] { "CustomerId", "OccurredAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerTransactions_BusinessRecords_BusinessRecordId",
                table: "CustomerTransactions",
                column: "BusinessRecordId",
                principalTable: "BusinessRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerTransactions_BusinessRecords_BusinessRecordId",
                table: "CustomerTransactions");

            migrationBuilder.DropTable(
                name: "CustomerActivities");

            migrationBuilder.DropIndex(
                name: "IX_CustomerTransactions_BusinessRecordId",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "BusinessRecordId",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "BusinessRecords");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "BusinessRecords");
        }
    }
}
