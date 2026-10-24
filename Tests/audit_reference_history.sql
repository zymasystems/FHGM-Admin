-- Disposable CI database only: simulate activity written before short references.
INSERT INTO "AuditLogs" ("UserId", "Action", "Entity", "EntityId", "CreatedAt") VALUES
('legacy-user', 'Login', 'User', 'legacy-user', NOW()),
('legacy-user', 'BlogCreated', 'BlogPost', 'legacy-blog', NOW()),
('legacy-user', 'Logout', 'User', 'legacy-user', NOW());
