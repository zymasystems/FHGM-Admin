# FHGM Website & Administration

For His Grace Ministries content platform. One repository, one solution, one ASP.NET Core project, with separate frontend and backend folders.

## Implementation status

This is **stage 1: accounts and the blog editorial workflow**, based on the September 2026 architecture document. It is not the complete CMS or a production deployment.

Implemented:

- ASP.NET Core 10 API, PostgreSQL and ASP.NET Core Identity.
- Responsive admin sign-in, article list/editor, team accounts and audit log.
- All five architecture roles; API enforcement and writer ownership checks.
- Blog drafts, submission, changes requested, rejection, approval, publication, archive and restore.
- Optimistic concurrency to prevent one editor overwriting another's changes.
- Public blog listing/article pages, exposing published articles only.
- Super administrator account creation and activation/deactivation of non-super-admin accounts. Disabling an account invalidates existing sessions on the next request.
- Password hashing, lockout, cookie sessions, CSRF validation, basic login throttling, security headers and transactional editorial audit entries.
- Docker, private PostgreSQL networking, Nginx configuration template and GitHub Actions integration tests.

Still to build:

- R2 presigned uploads and completion verification, centralized media metadata, albums, photos, thumbnails, uploaded videos and YouTube integration.
- Testimonial submission and moderation, scheduled popups, events and page/settings management.
- Full ministry website sections and final client branding/content.
- Role reassignment, password reset via a verified email provider, initial password change, and optional MFA.
- Reviewed EF migrations, backup/restore automation, monitoring and hardened production rollout.

MediaManager and CommunicationsManager accounts can sign in; their module tools are not implemented yet. Blog content uses plain text; rich HTML, embedded media, featured images and relational category/tag administration are future work. Categories and tags are currently text fields. No files are uploaded to the VPS or R2 in this stage.

## Layout

```text
FHGM-Admin.sln
frontend/
  admin/                 HTML and JavaScript admin client
  public/                Public blog client
  shared/                Shared styles
backend/
  FHGM.Admin/
    FHGM.Admin.csproj     Only application project
    Controllers/         Auth, users, blogs, public blog, audit
    Data/                Identity and application DbContext
    Models/              Entities and validated request contracts
    Services/            Editorial state machine, audit, initial setup
deploy/nginx/            Host Nginx HTTPS proxy template
tests/integration.py     HTTP checks against a disposable PostgreSQL database
```

## Quick start with Docker

Requirements: Docker Engine/Desktop with Compose. Run these from the repository root:

```bash
cp .env.example .env
```

Edit `.env`: set a unique database password, your administrator email and a strong initial password (12+ characters with uppercase, lowercase, number and symbol). Never commit this file. Avoid `;` in the database password because it is embedded in a connection string. Docker Compose also interprets `$`; use a correctly quoted `.env` value when needed.

```bash
docker compose up -d db
docker compose build app
docker compose run --rm app --initialize
docker compose up -d app
```

- Admin: http://localhost:5080/admin/
- Public blog: http://localhost:5080/public/
- Readiness: http://localhost:5080/health/ready

Sign in using the email/password you set. Create a Writer and BlogEditor from **Team members**. A writer saves and submits an article; an editor approves and then publishes it. Visit the public blog to see it. Admin login and the public website use the same origin; keep API calls relative to `/api/`.

`--initialize` is an explicit first-install operation. It creates the initial schema and roles, then the first SuperAdmin only if the database has no users. Do not run initialization concurrently. Existing accounts are never reset from environment values. Remove bootstrap credentials from the app environment after first setup (also adjust Compose's required bootstrap variables if removing them from `.env`).

Data persists in named PostgreSQL and Data Protection volumes. `docker compose down` retains it; **`docker compose down -v` deletes it**. Back up the database and keep Data Protection keys private.

## Run from Visual Studio or the .NET CLI

Install the .NET 10 SDK and PostgreSQL. Open **FHGM-Admin.sln**, with **FHGM.Admin** as the startup project. Configure local secrets from the root:

```bash
dotnet user-secrets set 'ConnectionStrings:Default' 'Host=localhost;Database=fhgm;Username=fhgm;Password=YOUR_DB_PASSWORD' --project backend/FHGM.Admin
dotnet user-secrets set 'Bootstrap:Email' 'YOUR_ADMIN_EMAIL' --project backend/FHGM.Admin
dotnet user-secrets set 'Bootstrap:Password' 'YOUR_STRONG_PASSWORD' --project backend/FHGM.Admin
dotnet run --project backend/FHGM.Admin -- --initialize
dotnet run --project backend/FHGM.Admin
```

Create the PostgreSQL database/user first. Keep passwords out of shell history where possible. Frontend files are copied to the output/publish `wwwroot` by the project, so rerun/build after editing them. Opening the HTML files directly will not work: they need the API origin.

## API surface

| Endpoint | Access |
| --- | --- |
| `GET /api/auth/csrf` | Anonymous; get token before POST/PUT |
| `POST /api/auth/login` | Anonymous with CSRF token |
| `GET /api/auth/me`, `POST /api/auth/logout` | Signed-in accounts |
| `GET/POST /api/blog` | Writer, BlogEditor, SuperAdmin |
| `GET/PUT /api/blog/{id}` | Own editable posts for writers; editors can review |
| `POST /api/blog/{id}/workflow` | Submit for writers; editorial actions for editors/admin |
| `GET /api/public/blog[?page=1]`, `GET /api/public/blog/{slug}` | Anonymous; published posts only |
| `GET/POST /api/users`, `PUT /api/users/{id}/active` | SuperAdmin |
| `GET /api/audit[?page=1]` | SuperAdmin |

Mutating requests require the `X-CSRF-TOKEN` header and its paired cookie. Refresh the token after login. No CORS or bearer token authentication is configured. Workflow actions: `submit`, `request-changes`, `reject`, `approve`, `publish`, `archive`, `restore`. Edits and workflow requests carry the current `version`; stale changes return 409. Published/approved content cannot be edited directly. Archive, restore, revise and resubmit it for approval. Archiving removes it from public endpoints immediately.

## Verification

```bash
dotnet build FHGM-Admin.sln -c Release
node --check frontend/admin/app.js
node --check frontend/public/app.js
```

CI builds the solution, initializes an isolated PostgreSQL service, starts the API and runs `python3 tests/integration.py`. The test creates accounts and content: **use a disposable database only**. Set `Bootstrap__Email`, `Bootstrap__Password` and optionally `FHGM_TEST_URL` when running it locally against an initialized test instance. It exercises anonymous access, CSRF, roles, writer ownership, duplicate slugs, stale updates, editorial transitions, public visibility, audit entries and disabled sessions.

## VPS preparation

1. Configure DNS and obtain HTTPS certificates covering the public and admin hostnames.
2. Review `deploy/nginx/fhgm.conf.example`; install it in host Nginx using the actual certificate paths. Both hostnames proxy the same app and their own relative `/api/` routes. The API subdomain in the architecture is deferred until a deliberate cross-origin authentication design is added.
3. Initialize the database as above, then run `docker compose -f compose.yaml -f compose.production.yaml up -d --build`. Production requires HTTPS because session/CSRF cookies are Secure. Keep `5080` bound to loopback and do not publish the database port.
4. Configure backups/restore testing, monitoring and a delivery process before accepting live client content.

**First-release limitations:** initialization uses `EnsureCreated`, not EF migrations. Do not use it to upgrade a populated database; introduce a reviewed initial migration/baseline before the next schema change. Audit IPs and login throttling currently use the direct connection address; behind the proxy that address is shared. Configure a narrowly trusted forwarded-header network and distributed rate limits before a wider rollout. Password recovery/email delivery is not yet implemented. Client-rendered blog pages need further work for server-rendered SEO/social previews. The hostnames in the template are from the specification; verify their spelling, ownership and certificates before deployment.
