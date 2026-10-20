using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewslettersAndResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Newsletters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AudienceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AudienceIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_Newsletters", x => x.Id);
                    table.CheckConstraint("CK_Newsletters_Audience", "[AudienceType] IN (N'all', N'room', N'program', N'group', N'label')");
                    table.CheckConstraint("CK_Newsletters_Status", "[Status] IN (N'draft', N'scheduled', N'published', N'archived')");
                    table.ForeignKey(
                        name: "FK_Newsletters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ResourceFolders", x => x.Id);
                    table.CheckConstraint("CK_ResourceFolders_Audience", "[Audience] IN (N'parents', N'staff')");
                    table.ForeignKey(
                        name: "FK_ResourceFolders_ResourceFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "ResourceFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceFolders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NewsletterLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsletterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkIndex = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_NewsletterLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterLinks_Newsletters_NewsletterId",
                        column: x => x.NewsletterId,
                        principalTable: "Newsletters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsletterLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NewsletterRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsletterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_NewsletterRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterRecipients_Newsletters_NewsletterId",
                        column: x => x.NewsletterId,
                        principalTable: "Newsletters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsletterRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ResourceDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceDocuments_ResourceFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "ResourceFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NewsletterClicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsletterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkIndex = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_NewsletterClicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterClicks_NewsletterRecipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "NewsletterRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsletterClicks_Newsletters_NewsletterId",
                        column: x => x.NewsletterId,
                        principalTable: "Newsletters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsletterClicks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ResourceVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceVersions_ResourceDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ResourceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ResourceDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceDownloads_ResourceDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ResourceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceDownloads_ResourceVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ResourceVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceDownloads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterClicks_NewsletterId_LinkIndex",
                table: "NewsletterClicks",
                columns: new[] { "NewsletterId", "LinkIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterClicks_RecipientId",
                table: "NewsletterClicks",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterClicks_TenantId",
                table: "NewsletterClicks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterLinks_NewsletterId_LinkIndex",
                table: "NewsletterLinks",
                columns: new[] { "NewsletterId", "LinkIndex" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterLinks_TenantId",
                table: "NewsletterLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterRecipients_NewsletterId_UserId",
                table: "NewsletterRecipients",
                columns: new[] { "NewsletterId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterRecipients_TenantId",
                table: "NewsletterRecipients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterRecipients_Token",
                table: "NewsletterRecipients",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Newsletters_TenantId_Status_ScheduledFor",
                table: "Newsletters",
                columns: new[] { "TenantId", "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceDocuments_FolderId",
                table: "ResourceDocuments",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceDocuments_TenantId_FolderId",
                table: "ResourceDocuments",
                columns: new[] { "TenantId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceDownloads_DocumentId",
                table: "ResourceDownloads",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceDownloads_TenantId_DocumentId_CreatedAt",
                table: "ResourceDownloads",
                columns: new[] { "TenantId", "DocumentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceDownloads_VersionId",
                table: "ResourceDownloads",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceFolders_ParentFolderId",
                table: "ResourceFolders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceFolders_TenantId",
                table: "ResourceFolders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVersions_DocumentId_Version",
                table: "ResourceVersions",
                columns: new[] { "DocumentId", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVersions_TenantId",
                table: "ResourceVersions",
                column: "TenantId");

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterClicks");

            migrationBuilder.DropTable(
                name: "NewsletterLinks");

            migrationBuilder.DropTable(
                name: "ResourceDownloads");

            migrationBuilder.DropTable(
                name: "NewsletterRecipients");

            migrationBuilder.DropTable(
                name: "ResourceVersions");

            migrationBuilder.DropTable(
                name: "Newsletters");

            migrationBuilder.DropTable(
                name: "ResourceDocuments");

            migrationBuilder.DropTable(
                name: "ResourceFolders");
        }
    }
}
