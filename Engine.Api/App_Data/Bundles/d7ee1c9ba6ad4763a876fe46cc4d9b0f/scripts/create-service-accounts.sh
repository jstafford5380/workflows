#!/usr/bin/env sh
set -eu

REQUEST_FILE="${1:?request json path is required}"
PROJECT_NUMBER=$(awk -F'"' '/"projectNumber"[[:space:]]*:/ {print $4; exit}' "$REQUEST_FILE")
if [ -z "$PROJECT_NUMBER" ]; then
  PROJECT_NUMBER="p-000000000"
fi

COUNT=$(( ( $(printf "%s" "$PROJECT_NUMBER" | cksum | awk '{print $1}') % 3 ) + 2 ))
printf '{"projectNumber":"%s","serviceAccountCount":%d}\n' "$PROJECT_NUMBER" "$COUNT"
