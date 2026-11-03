#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname -- "$0")/../.." && pwd)
backup=${1:?Pass the directory of a completed backup}
(cd "$backup" && sha256sum -c SHA256SUMS)
compose=(docker compose --env-file "${FHGM_ENV_FILE:-$repo/.env}" -f "$repo/deploy/docker-compose.yml")
# Always a new isolated database. Never accepts a target database name.
check_db="fhgm_restore_test_$(date -u +%s)_$RANDOM"
"${compose[@]}" exec -T db createdb -U fhgm "$check_db"
trap '"${compose[@]}" exec -T db dropdb -U fhgm --if-exists "$check_db"' EXIT
"${compose[@]}" exec -T db pg_restore -U fhgm -d "$check_db" --exit-on-error --no-owner < "$backup/database.dump"
"${compose[@]}" exec -T db psql -U fhgm -d "$check_db" -v ON_ERROR_STOP=1 -c 'SELECT COUNT(*) FROM "AspNetUsers"; SELECT COUNT(*) FROM "GalleryImages"; SELECT COUNT(*) FROM "__EFMigrationsHistory";'
echo 'Restore test passed in an isolated database.'
