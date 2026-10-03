using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class ImproveSalesApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_SalesQuotationId",
                table: "SalesOrders");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "SalesQuotations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "SalesQuotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "SalesQuotations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "SalesQuotations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastRevisionReason",
                table: "SalesQuotations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedByUserId",
                table: "SalesQuotations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "SalesQuotations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalesQuotations",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "SalesQuotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SalesQuotationId",
                table: "SalesOrders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "OpportunityId",
                table: "SalesOrders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "SalesOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "SalesOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "SalesOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastRevisionReason",
                table: "SalesOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedByUserId",
                table: "SalesOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "SalesOrders",
                type: "nvarchar(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "SalesOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalesOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "SalesOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentActivities_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ActionByUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    FinancialTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentApprovals_Users_ActionByUserId",
                        column: x => x.ActionByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentApprovals_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SalesQuotationId",
                table: "SalesOrders",
                column: "SalesQuotationId",
                unique: true,
                filter: "[SalesQuotationId] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_DocumentType_DocumentId_PerformedAt",
                table: "DocumentActivities",
                columns: new[] { "DocumentType", "DocumentId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_PerformedByUserId",
                table: "DocumentActivities",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovals_ActionByUserId",
                table: "DocumentApprovals",
                column: "ActionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovals_DocumentType_DocumentId_RevisionNumber",
                table: "DocumentApprovals",
                columns: new[] { "DocumentType", "DocumentId", "RevisionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovals_SubmittedByUserId",
                table: "DocumentApprovals",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_CreatedByUserId",
                table: "DocumentRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_DocumentType_DocumentId_RevisionNumber",
                table: "DocumentRevisions",
                columns: new[] { "DocumentType", "DocumentId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentActivities");

            migrationBuilder.DropTable(
                name: "DocumentApprovals");

            migrationBuilder.DropTable(
                name: "DocumentRevisions");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_SalesQuotationId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "LastRevisionReason",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "LastRevisionReason",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "SalesOrders");

            migrationBuilder.AlterColumn<int>(
                name: "SalesQuotationId",
                table: "SalesOrders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OpportunityId",
                table: "SalesOrders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SalesQuotationId",
                table: "SalesOrders",
                column: "SalesQuotationId",
                unique: true,
                filter: "[IsActive] = 1");
        }
    }
}
