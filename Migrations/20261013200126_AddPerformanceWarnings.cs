using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReportedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignedHrUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceCases_Users_AssignedHrUserId",
                        column: x => x.AssignedHrUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceCases_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformanceCaseId = table.Column<int>(type: "int", nullable: false),
                    Objectives = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    MeasurableExpectations = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_PerformanceCases_PerformanceCaseId",
                        column: x => x.PerformanceCaseId,
                        principalTable: "PerformanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceMeetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformanceCaseId = table.Column<int>(type: "int", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MeetingTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DiscussionNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceMeetings_PerformanceCases_PerformanceCaseId",
                        column: x => x.PerformanceCaseId,
                        principalTable: "PerformanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceMeetings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceWarnings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformanceCaseId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponseRemarks = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceWarnings_PerformanceCases_PerformanceCaseId",
                        column: x => x.PerformanceCaseId,
                        principalTable: "PerformanceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceWarnings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_CreatedByUserId",
                table: "ImprovementPlans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_PerformanceCaseId",
                table: "ImprovementPlans",
                column: "PerformanceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_ReviewDate",
                table: "ImprovementPlans",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_Status",
                table: "ImprovementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCases_AssignedHrUserId",
                table: "PerformanceCases",
                column: "AssignedHrUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCases_CaseNumber",
                table: "PerformanceCases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCases_CreatedByUserId",
                table: "PerformanceCases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCases_EmployeeId",
                table: "PerformanceCases",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCases_ReportedDate",
                table: "PerformanceCases",
                column: "ReportedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCases_Status",
                table: "PerformanceCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMeetings_CreatedByUserId",
                table: "PerformanceMeetings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMeetings_MeetingDate",
                table: "PerformanceMeetings",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMeetings_PerformanceCaseId",
                table: "PerformanceMeetings",
                column: "PerformanceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMeetings_Status",
                table: "PerformanceMeetings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceWarnings_CreatedByUserId",
                table: "PerformanceWarnings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceWarnings_IssueDate",
                table: "PerformanceWarnings",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceWarnings_PerformanceCaseId",
                table: "PerformanceWarnings",
                column: "PerformanceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceWarnings_Status",
                table: "PerformanceWarnings",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImprovementPlans");

            migrationBuilder.DropTable(
                name: "PerformanceMeetings");

            migrationBuilder.DropTable(
                name: "PerformanceWarnings");

            migrationBuilder.DropTable(
                name: "PerformanceCases");
        }
    }
}
