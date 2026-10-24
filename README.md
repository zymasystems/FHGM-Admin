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

Implemented: sign-in/logout, developer/church roles and specialist roles, gallery albums and direct-to-R2 image uploads, descriptions, publishing/unpublishing and removal, church events with automatic public expiry, scheduled popup announcements, developer monitoring, team account creation and non-super-admin activation/deactivation, blog drafts/submission/review/rejection/approval/publication/archive/restore, published-only public feed/preview, stale-edit protection, CSRF validation, password hashing/lockout, audit records, EF migrations and application/dependency health checks.

Still to build: videos/YouTube, testimonials, page/settings management, password recovery/initial-password change/MFA and role reassignment. The gallery widget is supplied below for connecting the existing website; it has not been installed on the live site. Categories/tags remain text fields and blog bodies are plain text. ChurchAdmin, MediaManager and SuperAdmin can manage photos. CommunicationsManager can manage events and popup announcements.

## Docker quick start

From the repository root, copy `.env.example` to `.env` (`Copy-Item .env.example .env` in PowerShell, or `cp .env.example .env` in Bash). Set a unique PostgreSQL password and your initial administrator email/password. Passwords require 12+ characters, uppercase, lowercase, a number and a symbol. Keep `.env` private. Avoid semicolons in the database password because it is used in a connection string; quote any dollar signs correctly for Compose.

```bash
docker compose --env-file .env -f deploy/docker-compose.yml up -d db
docker compose --env-file .env -f deploy/docker-compose.yml build
docker compose --env-file .env -f deploy/docker-compose.yml run --rm backend --initialize
docker compose --env-file .env -f deploy/docker-compose.yml up -d
```

Open **http://localhost:5080/** and sign in with the configured initial account. Use **Photo gallery** to manage albums and upload images, **Blog articles** to write or review, **Team members** to add users and **Activity log** to inspect actions. `/public/gallery` previews the photo gallery; `/public/` is a preview of the blog feed, not a replacement for the ministry website.

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
| `/admin/gallery` | ChurchAdmin, MediaManager, SuperAdmin |
| `/admin/blogs` | ChurchAdmin, Writer, BlogEditor, SuperAdmin |
| `/admin/users`, `/admin/audit`, `/admin/monitoring` | Developer SuperAdmin only |
| `/admin/events`, `/admin/popups` | ChurchAdmin, CommunicationsManager, SuperAdmin |
| `/public/` | Anonymous CMS blog preview |
| `GET /api/auth/csrf`, `POST /api/auth/login` | Anonymous; POST needs the paired CSRF header/cookie |
| `GET /api/auth/me`, `POST /api/auth/logout` | Authenticated |
| `GET/POST /api/blog`, `GET/PUT /api/blog/{id}` | Blog team; writer ownership/stage checks |
| `POST /api/blog/{id}/workflow` | Submit for writer; review/publish actions for editor/admin |
| `GET /api/public/blog`, `GET /api/public/blog/{slug}` | Published articles only |
| `GET/POST /api/users`, `PUT /api/users/{id}/active` | Account management policy |
| `GET /api/audit` | Audit read policy |
| `/api/gallery/albums`, `/api/gallery/uploads`, `/api/gallery/images/{id}` | Gallery management policy |
| `GET /api/public/gallery/albums`, `GET /api/public/gallery/albums/{slug}` | Published albums and completed/published photos only |

Mutations need `X-CSRF-TOKEN` and the paired cookie. Refresh the token after sign-in or a session change. Editing/workflow requests include the current `version`; stale writes return 409. Published content must be archived/restored before editing and reviewing again. Disabling an account invalidates API sessions and MVC access on the next request.

## Tests and delivery

```bash
dotnet build FHGM-Admin.sln -c Release
node --check Frontend/wwwroot/admin/app.js
node --check Frontend/wwwroot/public/app.js
```

CI builds both projects, starts PostgreSQL 17, applies migrations, runs both apps, and executes `python3 Tests/church_content_integration.py` through MVC using Moto as a disposable S3-compatible storage server. Checks cover page policies, cookies/session isolation, CSRF, API roles, ownership, editorial states, public visibility, audit entries, disabled accounts, signed image uploads, file signatures, publication, unpublishing, removal and upload URL replay protection. `Tests/church_content_integration.py` includes the core and gallery suites automatically, then verifies church/developer permissions, event expiry and popup schedules. CI also validates Compose and builds both images. The integration test creates accounts/content: use a disposable database only. Set `Bootstrap__Email` and `Bootstrap__Password` to that test instance's account; optionally set `FHGM_TEST_URL` (default 5080) and `FHGM_BACKEND_TEST_URL` (default 5081).

`deploy.yml` is a **manual, main-only deployment workflow**. Configure the protected `production` environment, required reviewers, `VPS_PATH` variable and secrets `VPS_HOST`, `VPS_USER`, `VPS_SSH_PRIVATE_KEY`, `VPS_SSH_KNOWN_HOSTS`. Prepare the checkout, private `.env`, backups and Nginx/certificates on the VPS first. The workflow deploys the selected main commit, builds both services, applies migrations and checks readiness. It does not roll back schema changes automatically. No VPS deployment has been performed by adding this workflow.

## Nginx, existing website and monitoring

Use `deploy/nginx/fhgm.conf.example` for **admin.hisgraceministries.co.za** with real HTTPS certificate paths. Set `APP_ENVIRONMENT=Production` in `.env` before production startup. Preserve the public website's existing vhost. The optional public API location exposes published blog GET endpoints on the existing website's origin; add client UI integration separately. Do not expose administrative API routes publicly on the ministry site or enable broad CORS with credentials.

Both apps expose `/health/live` and `/health/ready` using ASP.NET Core health checks. Backend readiness verifies the database/schema; MVC readiness checks backend readiness. Enable Uptime Kuma with:

```bash
docker compose --env-file .env -f deploy/docker-compose.yml --profile monitoring up -d uptime-kuma
```

Kuma binds to `127.0.0.1:3001`; access through an SSH tunnel or a separately secured admin vhost. Complete its initial account setup. Add HTTP monitors for `http://frontend:8080/health/ready` and `http://backend:8080/health/ready`, plus your actual public website URL. Configure notification channels there; no alert credentials are shipped.

Forwarded headers are accepted only from explicitly trusted proxy addresses. Configure the host proxy address for accurate MVC request metadata. Compose assigns MVC `172.30.48.10` on the private network and the backend trusts only that proxy address for the forwarded scheme/client IP. If that subnet conflicts with your VPS, change both the network allocation and backend trusted address together. Configure `NGINX_PROXY_ADDRESS` to the exact host proxy peer address so HTTPS is recognized through both hops; secure CSRF cookies require it. Health and sign-in checks should be verified through real HTTPS before rollout. Add backups/restore checks, operational alerts and production hardening before storing live client content.


## Gallery setup and staff workflow

The gallery is intended for FHGM staff to maintain the website without editing HTML, pushing to GitHub or asking a developer to add each photo.

An administrator must do the one-time setup:

1. Create a Cloudflare R2 bucket and a token scoped to **Object Read & Write for that bucket only**.
2. Set `R2_ACCOUNT_ID`, `R2_BUCKET`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` and `R2_PUBLIC_BASE_URL` in the private `.env`. Use a public media custom domain for the final public image URLs, such as your own configured media subdomain. Do not use the S3 API endpoint as the public image URL.
3. Apply `deploy/r2-cors.json` to the bucket. Replace origins with the exact portal origins, without trailing slashes; remove the localhost origin in production. The browser's presigned PUT must send the same Content-Type used when requesting the upload.
4. Add an R2 lifecycle rule deleting objects under `staging/` after one day, as a fallback for abandoned uploads or deleted records. The backend also retries cleanup of removed/expired uploads and completed staging objects every five minutes.
5. Create **MediaManager** accounts for staff who should upload and publish gallery photos. Writers do not have gallery permissions.
6. Connect the public website once using the widget below, then verify uploads/publication through the real portal, R2 bucket and website before giving staff access.

Staff workflow:

1. Open **Photo gallery** and create an album, such as Sunday Service or Youth Conference.
2. Select photos and choose **Upload to this album**. JPEG, PNG and WebP are supported, up to 10 MB each and 20 files per batch.
3. Add useful image descriptions (alt text), optional captions, and select **Publish photo**. Save each photo.
4. Select **Publish album on the website** and save. Photos appear only when both the album and photo are published.
5. Uncheck publication to hide a photo/album from the gallery, or remove a photo to queue storage deletion.

Uploads go straight from the browser to a short-lived R2 PUT URL, so the VPS does not store image files. The backend checks object size, content type and file signature. It conditionally copies verified bytes from a staging key to a separate final key; reusing an upload URL cannot overwrite a verified final image. Album/image changes and their audit records commit together. Failed/unfinished uploads remain hidden.

**Visibility:** unpublishing removes an image from public gallery feeds immediately. If the media bucket/domain is public, a previously known direct image URL can still be opened. Removal hides the photo immediately and queues R2 deletion after the upload URL expires; cached public copies may outlive that deletion. Use a private delivery design if direct URLs must be revocable. The current implementation verifies image signatures, not full decoding, malware scanning, resizing or EXIF removal.

## Connect the existing website gallery once

Review and include `deploy/nginx/gallery-location.conf.example` in the existing website HTTPS vhost. It exposes only anonymous gallery reads and the two widget assets. Keep admin routes on the portal host. In the gallery page, replace its hard-coded photo grid with:

```html
<link rel="stylesheet" href="/cms/gallery-widget.css">
<div data-fhgm-gallery data-api="/api/public/gallery"></div>
<script src="/cms/gallery-widget.js" defer></script>
```

The widget reads albums/photos from the API, includes pagination and lets visitors open full images. Subsequent staff uploads and publication changes update the gallery without further website source edits. If the website is hosted somewhere other than the VPS, configure the equivalent same-origin proxy or a deliberate read-only public API integration there. These instructions and assets do **not** modify the live website automatically.

Local storage tests use the loopback Moto emulator only: `python -m pip install -r Tests/requirements.txt`, start `python -m moto.server -H 127.0.0.1 -p 9002`, then `python Tests/setup_storage.py`. Set the Development-only R2 endpoint override and test settings shown in `.github/workflows/ci.yml` for the backend, plus matching non-secret media origins for MVC. Do not use emulator settings in production. Native PostgreSQL and both container builds are verified in CI; a real R2 connection still requires your own credentials, CORS and public domain setup.


## Church administrators and developer access

| Capability | Developer SuperAdmin | ChurchAdmin |
| --- | --- | --- |
| Gallery, blog creation/review/publication | Yes | Yes |
| Events: create, publish, edit, delete | Yes | Yes |
| Popup announcements: create, schedule, publish, delete | Yes | Yes |
| Create/view/enable/disable accounts | Yes | No |
| Application monitoring and audit logs | Yes | No |

The initial bootstrap account is a **developer SuperAdmin**. Sign in with it, open **Team members**, and create a **Church Admin** account for the church. The new account can manage church content but never sees developer navigation or receives access to developer MVC/API endpoints. Existing Writer, BlogEditor and MediaManager roles remain narrower specialist roles; CommunicationsManager handles events and popups. No existing account is automatically promoted or demoted.

**Activity log references** use persistent short labels such as `user-01`, `blog-02` and `event-03`. User entity references match the actor reference for the same account. The registry uses a shared database sequence, so numbers can have gaps; labels remain stable across pages, restarts and content deletion. Existing activity records are backfilled by the migration. Original IDs stay internal and are not returned by the audit API. These labels are display references, not credentials.

**Monitoring** shows dependency health, backend/container uptime and memory, R2 configuration status (not a live R2 availability probe) and account/content totals. It does not expose secrets or provide full VPS metrics. Use Uptime Kuma/server monitoring for external availability and host-wide metrics. Only developer SuperAdmins can open monitoring, audit records or account management.

## Events and popup notifications

Open **Events** to create an event with a title, URL name, description, location, start/end date-time and publication checkbox. The end must be after the start. Date-time fields use the browser device's displayed time zone, then are stored in UTC. The API includes published ongoing/future events only while `EndsAt > now`; an expired event detail returns 404. Expired records remain visible to church administrators for reference or manual deletion. Expiry does not require a cleanup job and does not permanently delete content. The public widget also removes cards as their end time passes.

Open **Popup announcements** to add a title, message, active start/end times, optional HTTPS/relative button link and priority. Only enabled announcements where `StartsAt <= now < EndsAt` are public. Higher priority items display first, one at a time. The widget remembers dismissals for the current browser session and announcement version. Editing an announcement makes the new version eligible again. It polls every minute for publication/schedule changes and closes expired popups on a one-second check. These are **website announcement popups**, not browser push notifications or emails.

Public preview: **http://localhost:5080/public/events**. Admin tools: `/admin/events`, `/admin/popups`, `/admin/monitoring`. The API routes are `/api/events`, `/api/popups`, `/api/monitoring`; public routes are `GET /api/public/events`, `GET /api/public/events/{slug}` and `GET /api/public/popups/active`. Updates and deletes require the current version; deletes pass `?version=N` to avoid removing a record that someone else changed.

To connect the existing public website, review `deploy/nginx/church-content-location.conf.example` and add the corresponding locations inside the website HTTPS vhost. Include these assets once on pages that should display announcements:

```html
<link rel="stylesheet" href="/cms/church-content.css">
<script src="/cms/church-content.js" data-popups-api="/api/public/popups/active" defer></script>
```

On the events page, also add:

```html
<div data-fhgm-events data-api="/api/public/events"></div>
```

The live website is not changed by adding these files to the portal repository. Once connected, church staff manage events and popups through the portal without editing website code.

### Update an existing local installation

Preserve your local code edits before pulling `codex/fhgm-foundation` (commit them or resolve any Git conflicts; do not discard them). Then rebuild and apply the new migration/role seed without deleting the existing database:

```powershell
git pull --ff-only
docker compose --env-file .env -f deploy/docker-compose.yml build
docker compose --env-file .env -f deploy/docker-compose.yml up -d db
docker compose --env-file .env -f deploy/docker-compose.yml run --rm backend --initialize
docker compose --env-file .env -f deploy/docker-compose.yml up -d
```

`--initialize` adds the ChurchAdmin role and applies the event/popup migration while preserving existing users/passwords/content. Reopen the portal and create the church account from **Team members** using your developer account. Do not use `down -v` for an upgrade; it removes stored data.
