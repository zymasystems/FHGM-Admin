# Operations setup

These files provide installable jobs. Adding them to GitHub does not configure or activate any VPS jobs, SMTP credentials or alert destinations.

## Deploy the portal changes

Back up the existing database first. The migration adds fields for initial-password changes, article format/images, event images and album covers. It preserves existing accounts/content. Existing accounts are not forced to change their passwords; newly created staff accounts are. Deploy the admin before the public website companion change.

The deployment workflow now runs `backend --initialize` after building and before starting the updated services. It does not delete volumes. Readiness fails while migrations are pending. If schema deployment fails, inspect the logs before restarting; do not use `down -v`.

## Recovery email and authenticator

Set SMTP_HOST, SMTP_PORT (default 587), SMTP_FROM, SMTP_USERNAME and SMTP_PASSWORD in the private deployment .env, then recreate backend. SMTP must support STARTTLS. Test a reset with a controlled real account. No live email is sent by CI; a delivered recovery email still requires the user to open the link and submit a new password. Reset links use the production admin domain and are passed in the URL fragment, not server logs. Recovery does not turn off an existing authenticator.

Staff use Password & security to change passwords and add a time-based authenticator by manually entering its setup key. Enabling requires a valid code. Store the ten one-use recovery codes privately. An account with neither its authenticator nor a recovery code requires a separately verified administrator recovery procedure; this release does not provide an unauthenticated MFA bypass. SMTP is optional; if not configured, recovery cannot send emails and the generic response deliberately does not disclose whether an account exists.

## Backups

1. Choose a root-owned directory outside the repository, for example `/var/backups/fhgm`. Backups contain private ministry data and Data Protection keys. Use encrypted storage and encrypted off-host copies; no cloud backup destination is provisioned by this repository.
2. Create `/etc/fhgm-ops.env` (mode 600) with `FHGM_BACKUP_DIR=/var/backups/fhgm` and `FHGM_ENV_FILE=/opt/fhgm/.env`. Optional `FHGM_ALERT_WEBHOOK` accepts a JSON `text` payload. Keep its URL secret.
3. Update each service's `/opt/fhgm` path to the actual checkout. Copy the service/timer files to `/etc/systemd/system/`, run `systemctl daemon-reload`, then enable `fhgm-backup.timer`, `fhgm-health.timer`, and `fhgm-restore-test.timer` with `systemctl enable --now`.
4. Run `systemctl start fhgm-backup.service`, then `systemctl start fhgm-restore-test.service`. Check both with `journalctl -u SERVICE_NAME`. The restore test uses a new temporary database and drops only that database; it never restores into the live database.
5. Verify the off-host copy and establish retention. Scripts intentionally never delete completed backup directories.

The default daily database/key backup runs at 02:00 UTC (04:00 South Africa). The restore test runs Sundays at 03:00 UTC (05:00 South Africa). These backups do not copy R2 image objects. Protect the media bucket separately, and retain its objects alongside database backups to recover a complete gallery.

Health checks run every five minutes against admin readiness and the public website, and check backup freshness (26 hours). Failures exit nonzero and optionally send a webhook. Repeated failures send repeated notifications; configure destination-side deduplication if needed. Also monitor systemd backup/restore service failures in your existing server monitoring. Uptime Kuma remains available for richer alert routing.

## R2 diagnostics

Developer Monitoring has an explicit Test R2 read/write connection button. It writes and removes a tiny object under `healthchecks/`, and tests object metadata. Add a one-day lifecycle cleanup for this prefix as a fallback if deletion fails. It confirms server credentials/permissions, not browser CORS. Keep the bucket CORS policy and the portal's exact origin configured for browser PUT uploads.

## Publishing behavior

- Gallery queues retain selected File objects only while the page is open. Retry creates a fresh signed URL after removing that queue item's incomplete record. Each photo reports progress and failure separately.
- Bulk actions affect selected photos on the current page; partial failures are reported individually. They use existing server role/version checks. Incomplete images cannot be published.
- A cover must be a completed, published image in the same album. Unpublishing/removing it automatically makes the public feed fall back to another published image.
- Valid editable articles autosave after three seconds of inactivity. A conflict pauses autosave. Reopen to review the current server version; do not overwrite another writer's work blindly. Markdown supports headings, bold, italics, lists and HTTPS links through a DOM-only renderer; raw HTML never executes. Plain-text articles retain their format. Preview uses the same renderer as the public website, within the portal's own theme.
- Featured-image fields accept an HTTPS URL. Use a published R2 image URL to match the portal's image CSP. File uploads for featured images are managed through Gallery.
- Weekly events create 1–52 independent occurrences, in fixed South African time. Editing/deleting one does not change the rest. A repeated save of a new series with the same slug returns a conflict instead of duplicating it.
