using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTerminationClearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TerminationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TerminationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProposedLastWorkingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    NoticePeriod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    DecisionRemarks = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerminationRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerminationRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerminationRequests_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClearanceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminationRequestId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ResponsibleDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OutstandingItems = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearanceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClearanceItems_TerminationRequests_TerminationRequestId",
                        column: x => x.TerminationRequestId,
                        principalTable: "TerminationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClearanceItems_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerminationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminationRequestId = table.Column<int>(type: "int", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsLetter = table.Column<bool>(type: "bit", nullable: false),
                    IsGenerated = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerminationDocuments_TerminationRequests_TerminationRequestId",
                        column: x => x.TerminationRequestId,
                        principalTable: "TerminationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TerminationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminationRequestId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerminationStatusHistories_TerminationRequests_TerminationRequestId",
                        column: x => x.TerminationRequestId,
                        principalTable: "TerminationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TerminationStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceItems_Category",
                table: "ClearanceItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceItems_CreatedByUserId",
                table: "ClearanceItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceItems_Status",
                table: "ClearanceItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClearanceItems_TerminationRequestId",
                table: "ClearanceItems",
                column: "TerminationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationDocuments_IsLetter",
                table: "TerminationDocuments",
                column: "IsLetter");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationDocuments_TerminationRequestId",
                table: "TerminationDocuments",
                column: "TerminationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_CreatedByUserId",
                table: "TerminationRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_DecidedByUserId",
                table: "TerminationRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_EmployeeId",
                table: "TerminationRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_ProposedLastWorkingDay",
                table: "TerminationRequests",
                column: "ProposedLastWorkingDay");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_ReferenceNumber",
                table: "TerminationRequests",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_RequestDate",
                table: "TerminationRequests",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_Status",
                table: "TerminationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationStatusHistories_ChangedAt",
                table: "TerminationStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationStatusHistories_ChangedByUserId",
                table: "TerminationStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationStatusHistories_TerminationRequestId",
                table: "TerminationStatusHistories",
                column: "TerminationRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClearanceItems");

            migrationBuilder.DropTable(
                name: "TerminationDocuments");

            migrationBuilder.DropTable(
                name: "TerminationStatusHistories");

            migrationBuilder.DropTable(
                name: "TerminationRequests");
        }
    }
}
