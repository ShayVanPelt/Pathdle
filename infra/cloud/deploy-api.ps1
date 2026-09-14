# Deploy Pathdle API to Cloud Run (Windows / PowerShell).
# Prerequisites: gcloud auth, project set, Artifact Registry repo, Secret Manager secret.
#
# Usage (from repo root):
#   $env:GCP_PROJECT = "your-project"
#   $env:GCP_REGION = "us-central1"
#   $env:AR_REPO = "pathdle"
#   $env:CORS_ORIGINS = "https://your-app.vercel.app,http://localhost:3000"
#   $env:PG_SECRET = "pathdle-pg"
#   .\infra\cloud\deploy-api.ps1

$ErrorActionPreference = "Stop"

function Require-Env([string]$Name) {
  $val = [Environment]::GetEnvironmentVariable($Name)
  if ([string]::IsNullOrWhiteSpace($val)) {
    throw "Set environment variable $Name"
  }
  return $val
}

$Project = Require-Env "GCP_PROJECT"
$Region = if ($env:GCP_REGION) { $env:GCP_REGION } else { "us-central1" }
$Repo = if ($env:AR_REPO) { $env:AR_REPO } else { "pathdle" }
$Service = if ($env:SERVICE) { $env:SERVICE } else { "pathdle-api" }
$Cors = Require-Env "CORS_ORIGINS"
$PgSecret = if ($env:PG_SECRET) { $env:PG_SECRET } else { "pathdle-pg" }

$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $Root

$Sha = (git rev-parse --short HEAD).Trim()
$Image = "$Region-docker.pkg.dev/$Project/$Repo/$Service"

Write-Host "==> Building ${Image}:latest"
gcloud builds submit `
  --project $Project `
  --config infra/cloud/cloudbuild.api.yaml `
  --substitutions="_REGION=$Region,_REPOSITORY=$Repo,_SERVICE=$Service,SHORT_SHA=$Sha"

Write-Host "==> Deploying Cloud Run service $Service"
gcloud run deploy $Service `
  --project $Project `
  --region $Region `
  --platform managed `
  --image "${Image}:latest" `
  --allow-unauthenticated `
  --port 8080 `
  --memory 512Mi `
  --cpu 1 `
  --min-instances 0 `
  --max-instances 5 `
  --set-env-vars "Pathdle__Storage=Postgres,ASPNETCORE_ENVIRONMENT=Production,Cors__AllowedOrigins=$Cors" `
  --set-secrets "ConnectionStrings__Postgres=${PgSecret}:latest"

$Url = gcloud run services describe $Service `
  --project $Project `
  --region $Region `
  --format="value(status.url)"

Write-Host ""
Write-Host "Deployed: $Url"
Write-Host "Health:   $Url/api/health"
Write-Host "Set NEXT_PUBLIC_API_BASE_URL=$Url on Vercel."
