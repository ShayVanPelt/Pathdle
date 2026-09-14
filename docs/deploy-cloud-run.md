# Deploy Pathdle API (and optionally Generator Job) to Google Cloud Run

This repo ships Dockerfiles and deploy helpers. There is no Terraform yet — use `gcloud`.

## Prerequisites

- [Google Cloud SDK](https://cloud.google.com/sdk/docs/install) (`gcloud`)
- Billing-enabled GCP project
- Supabase Postgres with schema applied (`infra/sql/supabase_bootstrap.sql`)
- At least one row in `daily_puzzles` for today (local generator publish is fine)

## One-time project setup

```bash
export GCP_PROJECT=your-project-id
export GCP_REGION=us-central1
export AR_REPO=pathdle

gcloud config set project "$GCP_PROJECT"

gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  cloudbuild.googleapis.com \
  secretmanager.googleapis.com

gcloud artifacts repositories create "$AR_REPO" \
  --repository-format=docker \
  --location="$GCP_REGION" \
  --description="Pathdle container images"
```

### Store the Supabase connection string

Use the same `ConnectionStrings__Postgres` value as local `.env` (SSL required). Prefer Secret Manager:

```bash
# PowerShell: pipe the string without echoing it into history if you can
# Bash:
printf '%s' 'Host=db.YOUR.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true' \
  | gcloud secrets create pathdle-pg --data-file=-

# Or update an existing secret:
# printf '%s' '...' | gcloud secrets versions add pathdle-pg --data-file=-
```

Grant the Cloud Run runtime service account access:

```bash
PROJECT_NUMBER="$(gcloud projects describe "$GCP_PROJECT" --format='value(projectNumber)')"
RUNTIME_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"

gcloud secrets add-iam-policy-binding pathdle-pg \
  --member="serviceAccount:${RUNTIME_SA}" \
  --role="roles/secretmanager.secretAccessor"
```

## Deploy the API (Cloud Run service)

From the **monorepo root**:

**PowerShell**

```powershell
$env:GCP_PROJECT = "your-project-id"
$env:GCP_REGION = "us-central1"
$env:AR_REPO = "pathdle"
$env:CORS_ORIGINS = "https://your-app.vercel.app,http://localhost:3000"
$env:PG_SECRET = "pathdle-pg"
.\infra\cloud\deploy-api.ps1
```

**Bash**

```bash
export GCP_PROJECT=your-project-id
export GCP_REGION=us-central1
export AR_REPO=pathdle
export CORS_ORIGINS=https://your-app.vercel.app,http://localhost:3000
export PG_SECRET=pathdle-pg
chmod +x infra/cloud/deploy-api.sh
./infra/cloud/deploy-api.sh
```

Or manually:

```bash
# Build (repo root)
gcloud builds submit --config infra/cloud/cloudbuild.api.yaml

# Deploy
gcloud run deploy pathdle-api \
  --region us-central1 \
  --image us-central1-docker.pkg.dev/$GCP_PROJECT/pathdle/pathdle-api:latest \
  --allow-unauthenticated \
  --port 8080 \
  --memory 512Mi \
  --min-instances 0 \
  --set-env-vars "Pathdle__Storage=Postgres,ASPNETCORE_ENVIRONMENT=Production,Cors__AllowedOrigins=https://your-app.vercel.app" \
  --set-secrets "ConnectionStrings__Postgres=pathdle-pg:latest"
```

### Verify

```bash
curl "$(gcloud run services describe pathdle-api --region us-central1 --format='value(status.url)')/api/health"
# expect: storage Postgres, database up
```

### Wire the web app

On Vercel (Production env):

```
NEXT_PUBLIC_API_BASE_URL=https://pathdle-api-xxxxx-uc.a.run.app
```

Local override for testing against cloud API: set the same in `apps/web/.env.local`.

## Local Docker smoke test (optional)

```bash
# From monorepo root
docker build -f apps/api/Dockerfile -t pathdle-api:local .

docker run --rm -p 8080:8080 \
  -e Pathdle__Storage=Postgres \
  -e ConnectionStrings__Postgres="Host=host.docker.internal;..." \
  -e Cors__AllowedOrigins=http://localhost:3000 \
  pathdle-api:local
```

## Generator Cloud Run Job (later)

Image: `apps/generator/Dockerfile`  
Build: `infra/cloud/cloudbuild.generator.yaml`

```bash
gcloud builds submit --config infra/cloud/cloudbuild.generator.yaml

gcloud run jobs create pathdle-generate \
  --region us-central1 \
  --image us-central1-docker.pkg.dev/$GCP_PROJECT/pathdle/pathdle-generator:latest \
  --memory 1Gi \
  --cpu 1 \
  --task-timeout 30m \
  --set-secrets "ConnectionStrings__Postgres=pathdle-pg:latest" \
  --set-env-vars "Pathdle__CorpusVersion=wiki-crawl-mvp-v1,Neo4j__Uri=...,Neo4j__Username=neo4j,Neo4j__Password=...,Neo4j__Database=neo4j" \
  --args="generate-daily"

# Manual run
gcloud run jobs execute pathdle-generate --region us-central1

# Daily schedule (tomorrow UTC is generator default)
gcloud scheduler jobs create http pathdle-daily \
  --location us-central1 \
  --schedule="15 0 * * *" \
  --uri="https://us-central1-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/$GCP_PROJECT/jobs/pathdle-generate:run" \
  --http-method POST \
  --oauth-service-account-email "$RUNTIME_SA"
```

Until Aura is ready, keep generating from your laptop against Supabase:

```bash
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --today
```

## Required production env

| Variable | Where | Notes |
|----------|--------|--------|
| `Pathdle__Storage` | Cloud Run | Must be `Postgres` (default JSON is InMemory) |
| `ConnectionStrings__Postgres` | Secret | Supabase SSL connection string |
| `Cors__AllowedOrigins` | Cloud Run | Vercel URL (+ localhost for debug) |
| `NEXT_PUBLIC_API_BASE_URL` | Vercel | Cloud Run service URL |

## Files

| Path | Role |
|------|------|
| `apps/api/Dockerfile` | API image |
| `apps/generator/Dockerfile` | Generator Job image |
| `infra/cloud/cloudbuild.api.yaml` | Cloud Build for API |
| `infra/cloud/cloudbuild.generator.yaml` | Cloud Build for generator |
| `infra/cloud/deploy-api.ps1` / `.sh` | Build + deploy API |
| `.dockerignore` | Slim build context |
