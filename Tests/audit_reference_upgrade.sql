-- Confirm history was backfilled and new writes reuse the same references.
BEGIN;
DO $$
DECLARE old_user bigint; old_blog bigint;
BEGIN
    SELECT "Id" INTO STRICT old_user FROM "AuditReferences" WHERE "Entity" = 'User' AND "Identifier" = 'legacy-user';
    SELECT "Id" INTO STRICT old_blog FROM "AuditReferences" WHERE "Entity" = 'BlogPost' AND "Identifier" = 'legacy-blog';
    IF old_user <> 1 OR old_blog <> 2 THEN RAISE EXCEPTION 'Historical reference order is incorrect'; END IF;

    INSERT INTO "AuditLogs" ("UserId", "Action", "Entity", "EntityId", "CreatedAt") VALUES
    ('legacy-user', 'Login', 'User', 'legacy-user', NOW()),
    ('legacy-user', 'BlogEdited', 'BlogPost', 'legacy-blog', NOW()),
    (NULL, 'SystemAction', 'BlogPost', 'new-blog', NOW());

    IF (SELECT COUNT(*) FROM "AuditReferences") <> 3 THEN RAISE EXCEPTION 'References were duplicated or not allocated'; END IF;
    IF NOT EXISTS (SELECT 1 FROM "AuditReferences" WHERE "Id" = old_user AND "Identifier" = 'legacy-user')
        OR NOT EXISTS (SELECT 1 FROM "AuditReferences" WHERE "Id" = old_blog AND "Identifier" = 'legacy-blog') THEN
        RAISE EXCEPTION 'References changed after new activity';
    END IF;
END;
$$;
ROLLBACK;
