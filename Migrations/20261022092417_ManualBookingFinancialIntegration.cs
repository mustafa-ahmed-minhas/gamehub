using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class ManualBookingFinancialIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_SalesOrderId",
                table: "Bookings");

            migrationBuilder.CreateTable(
                name: "DocumentEditHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEditHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentEditHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SalesOrderId",
                table: "Bookings",
                column: "SalesOrderId",
                unique: true,
                filter: "[SalesOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEditHistories_ChangedByUserId",
                table: "DocumentEditHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEditHistories_DocumentType_DocumentId_ChangedAt",
                table: "DocumentEditHistories",
                columns: new[] { "DocumentType", "DocumentId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentEditHistories");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_SalesOrderId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SalesOrderId",
                table: "Bookings",
                column: "SalesOrderId");
        }
    }
}
