using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSalesOrderLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalesOrderId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SalesOrderId",
                table: "Bookings",
                column: "SalesOrderId",
                unique: true,
                filter: "[SalesOrderId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_SalesOrders_SalesOrderId",
                table: "Bookings",
                column: "SalesOrderId",
                principalTable: "SalesOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_SalesOrders_SalesOrderId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_SalesOrderId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "Bookings");
        }
    }
}
