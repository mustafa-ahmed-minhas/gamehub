using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddHRModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobPostings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmploymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkplaceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JobLocation = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Vacancies = table.Column<int>(type: "int", nullable: false),
                    MinimumSalary = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    MaximumSalary = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExperienceRequired = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MinimumEducation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    JobDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    KeyResponsibilities = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Qualifications = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ApplicationDeadline = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HiringManagerUserId = table.Column<int>(type: "int", nullable: true),
                    Benefits = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPostings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPostings_Users_HiringManagerUserId",
                        column: x => x.HiringManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    JobPostingId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NationalIdNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CurrentAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    HighestEducation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CurrentEmployer = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CurrentJobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpectedSalary = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    NoticePeriod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CoverLetter = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CvFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CvOriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CvContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CvFileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPostingScreeningQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobPostingId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostingScreeningQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPostingScreeningQuestions_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobPostingSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobPostingId = table.Column<int>(type: "int", nullable: false),
                    Skill = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostingSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPostingSkills_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobApplicationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplicationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplicationDocuments_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationDocuments_JobApplicationId",
                table: "JobApplicationDocuments",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ApplicationNumber",
                table: "JobApplications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_CreatedByUserId",
                table: "JobApplications",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobPostingId",
                table: "JobApplications",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobPostingId_Email",
                table: "JobApplications",
                columns: new[] { "JobPostingId", "Email" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobPostingId_NationalIdNumber",
                table: "JobApplications",
                columns: new[] { "JobPostingId", "NationalIdNumber" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Status",
                table: "JobApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_ApplicationDeadline",
                table: "JobPostings",
                column: "ApplicationDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_CreatedByUserId",
                table: "JobPostings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Department",
                table: "JobPostings",
                column: "Department");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_HiringManagerUserId",
                table: "JobPostings",
                column: "HiringManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_ReferenceNumber",
                table: "JobPostings",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Status",
                table: "JobPostings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostingScreeningQuestions_JobPostingId",
                table: "JobPostingScreeningQuestions",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostingSkills_JobPostingId",
                table: "JobPostingSkills",
                column: "JobPostingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobApplicationDocuments");

            migrationBuilder.DropTable(
                name: "JobPostingScreeningQuestions");

            migrationBuilder.DropTable(
                name: "JobPostingSkills");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "JobPostings");
        }
    }
}
