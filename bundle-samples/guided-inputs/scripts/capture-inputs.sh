#!/usr/bin/env sh
set -eu

: "${OZ_OUTPUT:?OZ_OUTPUT env var is required}"

PROJECT_NAME="${1}"
PROJECT_TIER="${2}"
BILLING_ACCOUNT="${3}"
INSTANCE_COUNT="${4}"
ENABLE_BUDGET="${5:-}"
LABELS_JSON="${6:-{}}"
REGIONS_JSON="${7}"

if [ -z "${ENABLE_BUDGET}" ]; then
  ENABLE_BUDGET="false"
fi

printf 'projectName=%s\n' "${PROJECT_NAME}" >> "${OZ_OUTPUT}"
printf 'projectTier=%s\n' "${PROJECT_TIER}" >> "${OZ_OUTPUT}"
printf 'instanceCount=%s\n' "${INSTANCE_COUNT}" >> "${OZ_OUTPUT}"
printf 'enableBudget=%s\n' "${ENABLE_BUDGET}" >> "${OZ_OUTPUT}"
printf 'labelsJson=%s\n' "${LABELS_JSON}" >> "${OZ_OUTPUT}"
printf 'regionsJson=%s\n' "${REGIONS_JSON}" >> "${OZ_OUTPUT}"
printf 'summary=%s\n' "project=${PROJECT_NAME}; tier=${PROJECT_TIER}; instances=${INSTANCE_COUNT}; budget=${ENABLE_BUDGET}; billingConfigured=true" >> "${OZ_OUTPUT}"

echo "Captured typed inputs for ${PROJECT_NAME}"
echo "tier=${PROJECT_TIER}"
echo "labels=${LABELS_JSON}"
echo "regions=${REGIONS_JSON}"
echo "billingAccountConfigured=$( [ -n "${BILLING_ACCOUNT}" ] && echo true || echo false )"
