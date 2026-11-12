using FHGM.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace FHGM.Backend.Data.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261112100000_StandaloneGalleryImages")]
public sealed class StandaloneGalleryImages : Migration
{
    protected override void Up(MigrationBuilder m) => m.AlterColumn<Guid>("AlbumId", "GalleryImages", type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
    protected override void Down(MigrationBuilder m)
    {
        // Refuse to discard or silently regroup standalone photos during a downgrade.
        m.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"GalleryImages\" WHERE \"AlbumId\" IS NULL) THEN RAISE EXCEPTION 'Assign standalone photos to albums before downgrading'; END IF; END $$;");
        m.AlterColumn<Guid>("AlbumId", "GalleryImages", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
    }
}
