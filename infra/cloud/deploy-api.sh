#!/usr/bin/env bash
# Deploy Pathdle API to Cloud Run.
# Prerequisites: gcloud auth, project set, Artifact Registry repo, Secret Manager secret.
#
# Usage (from repo root):
#   export GCP_PROJECT=your-project
#   export GCP_REGION=us-central1
#   export AR_REPO=pathdle
#   export CORS_ORIGINS=https://www.example.com,https://example.com,http://localhost:3000
#   export PG_SECRET=pathdle-pg          # Secret Manager secret id
#   ./infra/cloud/deploy-api.sh
#
# CORS_ORIGINS is comma-separated frontend origins. Deploy maps them to
# Cors__AllowedOrigins__0, __1, ... (ASP.NET array binding).

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

: "${GCP_PROJECT:?Set GCP_PROJECT}"
: "${GCP_REGION:=us-central1}"
: "${AR_REPO:=pathdle}"
: "${SERVICE:=pathdle-api}"
: "${CORS_ORIGINS:?Set CORS_ORIGINS (comma-separated)}"
: "${PG_SECRET:=pathdle-pg}"

IMAGE="${GCP_REGION}-docker.pkg.dev/${GCP_PROJECT}/${AR_REPO}/${SERVICE}"

IFS=',' read -r -a ORIGINS <<< "${CORS_ORIGINS}"
ENV_PARTS=("Pathdle__Storage=Postgres" "ASPNETCORE_ENVIRONMENT=Production")
idx=0
for raw in "${ORIGINS[@]}"; do
  origin="$(echo "${raw}" | xargs)"
  [[ -z "${origin}" ]] && continue
  ENV_PARTS+=("Cors__AllowedOrigins__${idx}=${origin}")
  idx=$((idx + 1))
done
if [[ "${idx}" -eq 0 ]]; then
  echo "CORS_ORIGINS must list at least one origin" >&2
  exit 1
fi
SET_ENV_VARS="^|^$(IFS='|'; echo "${ENV_PARTS[*]}")"

echo "==> Building ${IMAGE}:latest"
gcloud builds submit \
  --project "${GCP_PROJECT}" \
  --config infra/cloud/cloudbuild.api.yaml \
  --substitutions="_REGION=${GCP_REGION},_REPOSITORY=${AR_REPO},_SERVICE=${SERVICE},SHORT_SHA=$(git rev-parse --short HEAD)"

echo "==> Deploying Cloud Run service ${SERVICE}"
gcloud run deploy "${SERVICE}" \
  --project "${GCP_PROJECT}" \
  --region "${GCP_REGION}" \
  --platform managed \
  --image "${IMAGE}:latest" \
  --allow-unauthenticated \
  --port 8080 \
  --memory 512Mi \
  --cpu 1 \
  --min-instances 0 \
  --max-instances 5 \
  --set-env-vars "${SET_ENV_VARS}" \
  --set-secrets "ConnectionStrings__Postgres=${PG_SECRET}:latest"

URL="$(gcloud run services describe "${SERVICE}" \
  --project "${GCP_PROJECT}" \
  --region "${GCP_REGION}" \
  --format='value(status.url)')"

echo ""
echo "Deployed: ${URL}"
echo "Health:   ${URL}/api/health"
echo "Set NEXT_PUBLIC_API_BASE_URL=${URL} on Vercel."
