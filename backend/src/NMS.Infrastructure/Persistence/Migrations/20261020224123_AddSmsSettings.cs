using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SmsEnabled",
                table: "CentreSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SmsMonthlyQuota",
                table: "CentreSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CentreSettings_SmsQuota",
                table: "CentreSettings",
                sql: "[SmsMonthlyQuota] BETWEEN 0 AND 100000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CentreSettings_SmsQuota",
                table: "CentreSettings");

            migrationBuilder.DropColumn(
                name: "SmsEnabled",
                table: "CentreSettings");

            migrationBuilder.DropColumn(
                name: "SmsMonthlyQuota",
                table: "CentreSettings");
        }
    }
}
