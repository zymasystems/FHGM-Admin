#!/usr/bin/env bash
set -euo pipefail
failed=0
for url in 'https://admin.hisgraceministries.co.za/health/ready' 'https://hisgraceministries.co.za/'; do
  if ! curl --fail --silent --show-error --max-time 15 --retry 1 "$url" >/dev/null; then failed=1; fi
done
if [[ -n ${FHGM_BACKUP_DIR:-} ]]; then
  recent=$(find "$FHGM_BACKUP_DIR" -maxdepth 1 -name last-success -mmin -1560 -print)
  [[ -n "$recent" ]] || failed=1
fi
if ((failed)); then
  echo 'FHGM availability or backup freshness check failed.' >&2
  if [[ -n ${FHGM_ALERT_WEBHOOK:-} ]]; then
    curl --fail --silent --show-error --max-time 15 -H 'Content-Type: application/json' --data '{"text":"FHGM availability or backup freshness check failed. Check the portal, public website and backup logs."}' "$FHGM_ALERT_WEBHOOK" >/dev/null
  fi
  exit 1
fi
