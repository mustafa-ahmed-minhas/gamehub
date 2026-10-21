using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameHub.Migrations
{
    /// <summary>
    /// The "Other" option was removed from the Gender enum. Gender values are
    /// persisted as strings, so any legacy row still holding "Other" would fail
    /// to materialize once the enum member no longer exists. Those rows are
    /// remapped to "PreferNotToSay" (the neutral option) before the new code
    /// runs. No schema change is involved.
    /// </summary>
    public partial class RemoveGenderOtherOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Users] SET [Gender] = 'PreferNotToSay' WHERE [Gender] = 'Other';");
            migrationBuilder.Sql("UPDATE [Customers] SET [Gender] = 'PreferNotToSay' WHERE [Gender] = 'Other';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Gender] = 'PreferNotToSay' WHERE [Gender] = 'Other';");
            migrationBuilder.Sql("UPDATE [JobApplications] SET [Gender] = 'PreferNotToSay' WHERE [Gender] = 'Other';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data loss is not recoverable: remapped rows cannot be distinguished
            // from genuine "Prefer Not To Say" values, so Down is a no-op.
        }
    }
}
