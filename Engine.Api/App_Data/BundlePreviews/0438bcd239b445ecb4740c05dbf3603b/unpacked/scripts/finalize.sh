#!/usr/bin/env sh
set -eu

REQUEST_FILE="${1:?request json path is required}"

if grep -Eq '"approved"[[:space:]]*:[[:space:]]*true' "$REQUEST_FILE"; then
  APPROVED=true
else
  APPROVED=false
fi

SERVICE_ACCOUNTS=$(awk -F'[: ,}]+' '/"serviceAccounts"[[:space:]]*:/ {print $3; exit}' "$REQUEST_FILE")
if [ -z "$SERVICE_ACCOUNTS" ]; then
  SERVICE_ACCOUNTS=0
fi

printf '{"result":"completed","approved":%s,"serviceAccounts":%s}\n' "$APPROVED" "$SERVICE_ACCOUNTS"
