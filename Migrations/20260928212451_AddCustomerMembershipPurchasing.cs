using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMembershipPurchasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentTransactionId",
                table: "CustomerMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MembershipInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerMembershipId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    CheckoutToken = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlanDescription = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ArenaName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ArenaPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ArenaEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ArenaAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MembershipNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipInvoices_CustomerMemberships_CustomerMembershipId",
                        column: x => x.CustomerMembershipId,
                        principalTable: "CustomerMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipInvoices_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_PaymentTransactionId",
                table: "CustomerMemberships",
                column: "PaymentTransactionId",
                unique: true,
                filter: "[PaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvoices_CheckoutToken",
                table: "MembershipInvoices",
                column: "CheckoutToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvoices_CustomerId",
                table: "MembershipInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvoices_CustomerMembershipId",
                table: "MembershipInvoices",
                column: "CustomerMembershipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvoices_InvoiceNumber",
                table: "MembershipInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvoices_PaymentTransactionId",
                table: "MembershipInvoices",
                column: "PaymentTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerMemberships_PaymentTransactions_PaymentTransactionId",
                table: "CustomerMemberships",
                column: "PaymentTransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerMemberships_PaymentTransactions_PaymentTransactionId",
                table: "CustomerMemberships");

            migrationBuilder.DropTable(
                name: "MembershipInvoices");

            migrationBuilder.DropIndex(
                name: "IX_CustomerMemberships_PaymentTransactionId",
                table: "CustomerMemberships");

            migrationBuilder.DropColumn(
                name: "PaymentTransactionId",
                table: "CustomerMemberships");
        }
    }
}
