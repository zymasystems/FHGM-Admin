using FHGM.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace FHGM.Backend.Data.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261112160000_BlogPendingRevision")]
public sealed class BlogPendingRevision : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<string>("PendingRevisionJson", "BlogPosts", type: "text", nullable: true);
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"BlogPosts\" WHERE \"PendingRevisionJson\" IS NOT NULL) THEN RAISE EXCEPTION 'Publish or resolve pending revisions before downgrading'; END IF; END $$;");
        m.DropColumn("PendingRevisionJson", "BlogPosts");
    }
}
