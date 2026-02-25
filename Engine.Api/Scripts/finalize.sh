#!/usr/bin/env sh
set -eu

: "${OZ_OUTPUT:?OZ_OUTPUT env var is required}"

APPROVED="${1}"
SERVICE_ACCOUNTS="${2}"

printf 'result=completed\n' >> "$OZ_OUTPUT"
printf 'approved=%s\n' "$APPROVED" >> "$OZ_OUTPUT"
printf 'serviceAccounts=%s\n' "$SERVICE_ACCOUNTS" >> "$OZ_OUTPUT"
