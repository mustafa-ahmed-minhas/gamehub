using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMembershipLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "MembershipInvoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPlanCode",
                table: "MembershipInvoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPlanName",
                table: "MembershipInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupersededByMembershipId",
                table: "CustomerMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_SupersededByMembershipId",
                table: "CustomerMemberships",
                column: "SupersededByMembershipId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerMemberships_CustomerMemberships_SupersededByMembershipId",
                table: "CustomerMemberships",
                column: "SupersededByMembershipId",
                principalTable: "CustomerMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerMemberships_CustomerMemberships_SupersededByMembershipId",
                table: "CustomerMemberships");

            migrationBuilder.DropIndex(
                name: "IX_CustomerMemberships_SupersededByMembershipId",
                table: "CustomerMemberships");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "MembershipInvoices");

            migrationBuilder.DropColumn(
                name: "PreviousPlanCode",
                table: "MembershipInvoices");

            migrationBuilder.DropColumn(
                name: "PreviousPlanName",
                table: "MembershipInvoices");

            migrationBuilder.DropColumn(
                name: "SupersededByMembershipId",
                table: "CustomerMemberships");
        }
    }
}
