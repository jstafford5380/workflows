#!/usr/bin/env sh
set -eu

: "${OZ_OUTPUT:?OZ_OUTPUT env var is required}"

SUMMARY="${1}"

printf 'result=guided-inputs-complete\n' >> "${OZ_OUTPUT}"
printf 'summary=%s\n' "${SUMMARY}" >> "${OZ_OUTPUT}"

echo "Finalize step summary: ${SUMMARY}"
