using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentIdsAndProgramAges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdPhotoMediaId",
                table: "Students",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAgeMonths",
                table: "Programs",
                type: "int",
                nullable: false,
                defaultValue: 120);

            migrationBuilder.AddColumn<int>(
                name: "MinAgeMonths",
                table: "Programs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmiratesId",
                table: "Guardians",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdPhotoMediaId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MaxAgeMonths",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "MinAgeMonths",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "EmiratesId",
                table: "Guardians");
        }
    }
}
