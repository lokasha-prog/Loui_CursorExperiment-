using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Lessons",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Lessons");
        }
    }
}
