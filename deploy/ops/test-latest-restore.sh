#!/usr/bin/env bash
set -euo pipefail
stamp=$(cat "${FHGM_BACKUP_DIR:?Set FHGM_BACKUP_DIR}/last-success")
[[ "$stamp" =~ ^[0-9]{8}T[0-9]{6}Z$ ]] || { echo 'Invalid backup stamp' >&2; exit 1; }
exec bash "$(dirname -- "$0")/test-restore.sh" "$FHGM_BACKUP_DIR/$stamp"
