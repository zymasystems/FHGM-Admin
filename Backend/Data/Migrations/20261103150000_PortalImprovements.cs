using FHGM.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace FHGM.Backend.Data.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261103150000_PortalImprovements")]
public sealed class PortalImprovements : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>("MustChangePassword", "AspNetUsers", type: "boolean", nullable: false, defaultValue: false);
        m.AddColumn<string>("ContentFormat", "BlogPosts", type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PlainText");
        m.AddColumn<string>("FeaturedImageUrl", "BlogPosts", type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: "");
        m.AddColumn<string>("FeaturedImageUrl", "Events", type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: "");
        m.AddColumn<Guid>("CoverImageId", "GalleryAlbums", type: "uuid", nullable: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn("MustChangePassword", "AspNetUsers"); m.DropColumn("ContentFormat", "BlogPosts");
        m.DropColumn("FeaturedImageUrl", "BlogPosts"); m.DropColumn("FeaturedImageUrl", "Events"); m.DropColumn("CoverImageId", "GalleryAlbums");
    }
}
