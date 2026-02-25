#!/usr/bin/env sh
set -eu

REQUEST_FILE="${1:?request json path is required}"
PROJECT_ID=$(awk -F'"' '/"projectId"[[:space:]]*:/ {print $4; exit}' "$REQUEST_FILE")
if [ -z "$PROJECT_ID" ]; then
  PROJECT_ID="unknown-project"
fi

CHECKSUM=$(printf "%s" "${WORKFLOW_IDEMPOTENCY_KEY:-default-key}" | cksum | awk '{print $1}')
PROJECT_NUMBER=$(printf "p-%09d" $((CHECKSUM % 1000000000)))

printf '{"projectNumber":"%s","projectId":"%s"}\n' "$PROJECT_NUMBER" "$PROJECT_ID"
