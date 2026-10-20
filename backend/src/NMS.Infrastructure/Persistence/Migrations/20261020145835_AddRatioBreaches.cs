using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRatioBreaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RatioBreaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StudentsPresent = table.Column<int>(type: "int", nullable: false),
                    StaffPresent = table.Column<int>(type: "int", nullable: false),
                    StaffRequired = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatioBreaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatioBreaches_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RatioBreaches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RatioBreaches_RoomId",
                table: "RatioBreaches",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RatioBreaches_TenantId_CreatedAt",
                table: "RatioBreaches",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RatioBreaches_TenantId_RoomId_ResolvedAt",
                table: "RatioBreaches",
                columns: new[] { "TenantId", "RoomId", "ResolvedAt" });

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY [Security].[TenantIsolationPolicy];");

            migrationBuilder.DropTable(
                name: "RatioBreaches");

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");
        }
    }
}
