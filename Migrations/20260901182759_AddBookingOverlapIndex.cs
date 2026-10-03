using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingOverlapIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CourtId_BookingDate_IsActive_Status_StartTime_EndTime",
                table: "Bookings",
                columns: new[] { "CourtId", "BookingDate", "IsActive", "Status", "StartTime", "EndTime" },
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_CourtId_BookingDate_IsActive_Status_StartTime_EndTime",
                table: "Bookings");
        }
    }
}
