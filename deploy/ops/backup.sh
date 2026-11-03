#!/usr/bin/env bash
set -euo pipefail
umask 077
repo=$(cd -- "$(dirname -- "$0")/../.." && pwd)
backup_dir=${FHGM_BACKUP_DIR:?Set FHGM_BACKUP_DIR to a private backup directory}
mkdir -p "$backup_dir"
exec 9>"$backup_dir/.backup.lock"
flock -n 9 || exit 0
stamp=$(date -u +%Y%m%dT%H%M%SZ)
compose=(docker compose --env-file "${FHGM_ENV_FILE:-$repo/.env}" -f "$repo/deploy/docker-compose.yml")
partial="$backup_dir/$stamp.partial"
mkdir "$partial"
trap 'rm -rf -- "$partial"' EXIT
"${compose[@]}" exec -T db pg_dump -U fhgm -d fhgm -Fc > "$partial/database.dump"
"${compose[@]}" exec -T backend tar -C /home/app/.aspnet -cf - DataProtection-Keys > "$partial/protection-keys.tar"
test -s "$partial/database.dump"
test -s "$partial/protection-keys.tar"
(cd "$partial" && sha256sum database.dump protection-keys.tar > SHA256SUMS)
mv -- "$partial" "$backup_dir/$stamp"
printf '%s\n' "$stamp" > "$backup_dir/last-success"
echo "Backup complete: $backup_dir/$stamp"
# No automatic deletion. Configure retention only after verifying off-host copies.
