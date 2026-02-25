#!/usr/bin/env sh
set -eu

: "${OZ_OUTPUT:?OZ_OUTPUT env var is required}"

PROJECT_ID="${1}"
CHECKSUM=$(printf "%s" "${WORKFLOW_IDEMPOTENCY_KEY:-default-key}" | cksum | awk '{print $1}')
PROJECT_NUMBER=$(printf "p-%09d" $((CHECKSUM % 1000000000)))

printf 'projectNumber=%s\n' "$PROJECT_NUMBER" >> "$OZ_OUTPUT"
printf 'projectId=%s\n' "$PROJECT_ID" >> "$OZ_OUTPUT"
