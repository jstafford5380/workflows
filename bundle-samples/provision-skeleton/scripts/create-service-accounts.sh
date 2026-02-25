#!/usr/bin/env sh
set -eu

: "${OZ_OUTPUT:?OZ_OUTPUT env var is required}"

PROJECT_NUMBER="${1}"
COUNT=$(( ( $(printf "%s" "$PROJECT_NUMBER" | cksum | awk '{print $1}') % 3 ) + 2 ))

printf 'projectNumber=%s\n' "$PROJECT_NUMBER" >> "$OZ_OUTPUT"
printf 'serviceAccountCount=%s\n' "$COUNT" >> "$OZ_OUTPUT"
