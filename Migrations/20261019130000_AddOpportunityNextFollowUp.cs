using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityNextFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpAt",
                table: "Opportunities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_NextFollowUpAt",
                table: "Opportunities",
                column: "NextFollowUpAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Opportunities_NextFollowUpAt",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "NextFollowUpAt",
                table: "Opportunities");
        }
    }
}

