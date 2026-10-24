using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FHGM.Backend.Data.Migrations;

public partial class AddAuditReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditReferences",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Entity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Identifier = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AuditReferences", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AuditReferences_Entity_Identifier", table: "AuditReferences",
            columns: new[] { "Entity", "Identifier" }, unique: true);

        // Preserve one reference per original identifier, including deleted content.
        migrationBuilder.Sql("""
            WITH identifiers AS (
                SELECT 'User' AS entity, "UserId" AS identifier, "Id" AS first_seen
                FROM "AuditLogs" WHERE "UserId" IS NOT NULL
                UNION ALL
                SELECT "Entity", "EntityId", "Id" FROM "AuditLogs"
            )
            INSERT INTO "AuditReferences" ("Entity", "Identifier")
            SELECT entity, identifier FROM identifiers
            GROUP BY entity, identifier
            ORDER BY MIN(first_seen), CASE WHEN entity = 'User' THEN 0 ELSE 1 END, entity, identifier;

            CREATE FUNCTION fhgm_register_audit_references() RETURNS trigger AS $$
            BEGIN
                IF NEW."UserId" IS NOT NULL THEN
                    INSERT INTO "AuditReferences" ("Entity", "Identifier")
                    SELECT 'User', NEW."UserId"
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "AuditReferences" WHERE "Entity" = 'User' AND "Identifier" = NEW."UserId"
                    ) ON CONFLICT ("Entity", "Identifier") DO NOTHING;
                END IF;

                INSERT INTO "AuditReferences" ("Entity", "Identifier")
                SELECT NEW."Entity", NEW."EntityId"
                WHERE NOT EXISTS (
                    SELECT 1 FROM "AuditReferences" WHERE "Entity" = NEW."Entity" AND "Identifier" = NEW."EntityId"
                ) ON CONFLICT ("Entity", "Identifier") DO NOTHING;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER fhgm_audit_references AFTER INSERT ON "AuditLogs"
            FOR EACH ROW EXECUTE FUNCTION fhgm_register_audit_references();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER fhgm_audit_references ON "AuditLogs";
            DROP FUNCTION fhgm_register_audit_references();
            """);
        migrationBuilder.DropTable(name: "AuditReferences");
    }
}
