using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FHGM.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GalleryAlbums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryAlbums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GalleryImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StagingKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    StagingCleaned = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalleryImages_GalleryAlbums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "GalleryAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryAlbums_Slug",
                table: "GalleryAlbums",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GalleryImages_AlbumId_IsComplete_IsPublished_IsDeleted",
                table: "GalleryImages",
                columns: new[] { "AlbumId", "IsComplete", "IsPublished", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryImages_StorageKey",
                table: "GalleryImages",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GalleryImages");

            migrationBuilder.DropTable(
                name: "GalleryAlbums");
        }
    }
}
