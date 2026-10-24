# FHGM Administration Portal

Administration portal for **https://hisgraceministries.co.za/**. The existing public ministry website stays separate. This repository contains **one solution and two application projects**.

## Structure

- `FHGM-Admin.sln` — opens both applications in Visual Studio.
- `Frontend/FHGM.Admin.csproj` — ASP.NET Core MVC, Razor views, Bootstrap 5.3.8 and JavaScript.
- `Frontend/Controllers`, `Views`, `ViewModels`, `Services`, `wwwroot`, `Program.cs` — presentation, MVC authorization, session validation and a private API client.
- `Backend/FHGM.Backend.csproj` — ASP.NET Core Web API.
- `Backend/Controllers`, `Data`, `Entities`, `DTOs`, `Services`, `Repositories`, `Authentication`, `Infrastructure`, `Program.cs` — endpoints, EF Core migrations, domain records, contracts, editorial rules, persistence, Identity/policies and health checks.
- `Tests` — HTTP integration tests exercising MVC and API boundaries.
- `deploy/docker-compose.yml` — separate frontend, backend and PostgreSQL containers; optional Uptime Kuma.
- `deploy/nginx` — administration vhost and an optional public blog feed location.
- `.github/workflows` — CI and a manually dispatched VPS deployment workflow.

The portal uses the ministry site's near-black, gold and ivory palette and its supplied logo. The dashboard layout and blog workflow from the initial implementation are preserved as Razor views/partials.

## Responsibility and request flow

The browser opens MVC on port **5080**. MVC renders Razor pages and relays relative `/api/` calls to the private backend on **5081** locally, or `http://backend:8080/` in Compose. Identity, PostgreSQL access, authorization policies, editorial rules and audit persistence live in the backend. The frontend has no database credentials and no reference to the backend project or EF packages.

The backend issues host-only HttpOnly session cookies through the MVC relay. The relay forwards each caller's cookie and CSRF header and returns Set-Cookie headers. HttpClient cookie storage and redirects are disabled to prevent sessions being shared between users. MVC verifies each page session with `/api/auth/me` and checks navigation policies; the backend independently enforces permissions and writer ownership. Production requires HTTPS. Never use a cross-user shared cookie jar or put Identity tokens in localStorage.

## Implemented and planned

Implemented: sign-in/logout, all five roles, team account creation and non-super-admin activation/deactivation, blog drafts/submission/review/rejection/approval/publication/archive/restore, published-only public feed/preview, stale-edit protection, CSRF validation, password hashing/lockout, audit records, EF migrations and application/dependency health checks.

Still to build: R2 presigned uploads and verification, media/albums/YouTube, testimonials, popups, events, page/settings management, password recovery/initial-password change/MFA, role reassignment, and connecting the existing website UI to the public API. `Infrastructure/R2Options.cs` and backend-only environment settings define the storage configuration; **uploads are not implemented yet**. Categories/tags remain text fields and blog bodies are plain text. MediaManager and CommunicationsManager can access the overview; their content tools are pending.

## Docker quick start

From the repository root, copy `.env.example` to `.env` (`Copy-Item .env.example .env` in PowerShell, or `cp .env.example .env` in Bash). Set a unique PostgreSQL password and your initial administrator email/password. Passwords require 12+ characters, uppercase, lowercase, a number and a symbol. Keep `.env` private. Avoid semicolons in the database password because it is used in a connection string; quote any dollar signs correctly for Compose.

```bash
docker compose --env-file .env -f deploy/docker-compose.yml up -d db
docker compose --env-file .env -f deploy/docker-compose.yml build
docker compose --env-file .env -f deploy/docker-compose.yml run --rm backend --initialize
docker compose --env-file .env -f deploy/docker-compose.yml up -d
```

Open **http://localhost:5080/** and sign in with the configured initial account. Use **Blog articles** to write or review, **Team members** to add users and **Activity log** to inspect actions. `/public/` is a preview of the CMS feed, not a replacement for the ministry website.

The backend and PostgreSQL have **no published host ports**. Only MVC is bound to `127.0.0.1:5080` for the host Nginx proxy. The private network isolates the database; backend egress is enabled for future R2/email connections. Data Protection keys and database contents persist in named volumes. `down -v` destroys the volumes and their data.

`--initialize` applies committed migrations and creates roles plus the first SuperAdmin only if no users exist. Run it once at a time. Existing passwords are never reset from bootstrap environment variables. Remove `BOOTSTRAP_EMAIL` and `BOOTSTRAP_PASSWORD` from `.env` after first setup; later migration runs work without them if an account already exists.

**Earlier prototype databases:** the old single-project version used EnsureCreated. A populated database from that version needs a reviewed migration baseline before using this release. Do not run the new migration against that schema blindly or delete client data; use a fresh disposable database for local testing, or back up and baseline an existing one.

## Visual Studio / .NET CLI

Install .NET 10 SDK and PostgreSQL. Create the database/user, then set backend user secrets:

```bash
dotnet user-secrets set 'ConnectionStrings:Default' 'Host=localhost;Database=fhgm;Username=fhgm;Password=YOUR_PASSWORD' --project Backend
dotnet user-secrets set 'Bootstrap:Email' 'YOUR_EMAIL' --project Backend
dotnet user-secrets set 'Bootstrap:Password' 'YOUR_STRONG_PASSWORD' --project Backend
dotnet run --project Backend -- --initialize
```

Open `FHGM-Admin.sln`. Set **multiple startup projects**, with both FHGM.Backend and FHGM.Admin set to Start. Backend runs at `http://localhost:5081`, MVC at `http://localhost:5080`. Alternatively, start `dotnet run --project Backend` and `dotnet run --project Frontend` in separate terminals. `Frontend/appsettings.json` holds the non-secret `Backend:BaseUrl`; override with `Backend__BaseUrl` in deployment. Use user secrets only in Development.

For migrations: `dotnet tool restore`, then `dotnet ef migrations add YourChange --project Backend --output-dir Data/Migrations`. Review generated SQL and back up the database before production updates. The explicit backend initialization command applies migrations; normal API startup does not alter the schema.

## Routes and permissions

| Route | Access |
| --- | --- |
| `/`, `/admin/` | Authenticated overview |
| `/account/login` | Anonymous sign-in |
| `/admin/blogs` | Writer, BlogEditor, SuperAdmin |
| `/admin/users`, `/admin/audit` | SuperAdmin |
| `/public/` | Anonymous CMS blog preview |
| `GET /api/auth/csrf`, `POST /api/auth/login` | Anonymous; POST needs the paired CSRF header/cookie |
| `GET /api/auth/me`, `POST /api/auth/logout` | Authenticated |
| `GET/POST /api/blog`, `GET/PUT /api/blog/{id}` | Blog team; writer ownership/stage checks |
| `POST /api/blog/{id}/workflow` | Submit for writer; review/publish actions for editor/admin |
| `GET /api/public/blog`, `GET /api/public/blog/{slug}` | Published articles only |
| `GET/POST /api/users`, `PUT /api/users/{id}/active` | Account management policy |
| `GET /api/audit` | Audit read policy |

Mutations need `X-CSRF-TOKEN` and the paired cookie. Refresh the token after sign-in or a session change. Editing/workflow requests include the current `version`; stale writes return 409. Published content must be archived/restored before editing and reviewing again. Disabling an account invalidates API sessions and MVC access on the next request.

## Tests and delivery

```bash
dotnet build FHGM-Admin.sln -c Release
node --check Frontend/wwwroot/admin/app.js
node --check Frontend/wwwroot/public/app.js
```

CI builds both projects, starts PostgreSQL 17, applies migrations, runs both apps, and executes `python3 Tests/integration.py` through MVC. Checks cover page policies, cookies/session isolation, CSRF, API roles, ownership, editorial states, public visibility, audit entries and disabled accounts. CI also validates Compose and builds both images. The integration test creates accounts/content: use a disposable database only. Set `Bootstrap__Email` and `Bootstrap__Password` to that test instance's account; optionally set `FHGM_TEST_URL` (default 5080) and `FHGM_BACKEND_TEST_URL` (default 5081).

`deploy.yml` is a **manual, main-only deployment workflow**. Configure the protected `production` environment, required reviewers, `VPS_PATH` variable and secrets `VPS_HOST`, `VPS_USER`, `VPS_SSH_PRIVATE_KEY`, `VPS_SSH_KNOWN_HOSTS`. Prepare the checkout, private `.env`, backups and Nginx/certificates on the VPS first. The workflow deploys the selected main commit, builds both services, applies migrations and checks readiness. It does not roll back schema changes automatically. No VPS deployment has been performed by adding this workflow.

## Nginx, existing website and monitoring

Use `deploy/nginx/fhgm.conf.example` for **admin.hisgraceministries.co.za** with real HTTPS certificate paths. Set `APP_ENVIRONMENT=Production` in `.env` before production startup. Preserve the public website's existing vhost. The optional public API location exposes published blog GET endpoints on the existing website's origin; add client UI integration separately. Do not expose administrative API routes publicly on the ministry site or enable broad CORS with credentials.

Both apps expose `/health/live` and `/health/ready` using ASP.NET Core health checks. Backend readiness verifies the database/schema; MVC readiness checks backend readiness. Enable Uptime Kuma with:

```bash
docker compose --env-file .env -f deploy/docker-compose.yml --profile monitoring up -d uptime-kuma
```

Kuma binds to `127.0.0.1:3001`; access through an SSH tunnel or a separately secured admin vhost. Complete its initial account setup. Add HTTP monitors for `http://frontend:8080/health/ready` and `http://backend:8080/health/ready`, plus your actual public website URL. Configure notification channels there; no alert credentials are shipped.

Forwarded headers are accepted only from explicitly trusted proxy addresses. Configure the host proxy address for accurate MVC request metadata. Compose assigns MVC `172.30.48.10` on the private network and the backend trusts only that proxy address for the forwarded scheme/client IP. If that subnet conflicts with your VPS, change both the network allocation and backend trusted address together. Configure `NGINX_PROXY_ADDRESS` to the exact host proxy peer address so HTTPS is recognized through both hops; secure CSRF cookies require it. Health and sign-in checks should be verified through real HTTPS before rollout. Add backups/restore checks, operational alerts and production hardening before storing live client content.
