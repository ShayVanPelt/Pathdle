# Deploy Pathdle API (and optionally Generator Job) to Google Cloud Run

This repo ships Dockerfiles and deploy helpers. There is no Terraform yet — use `gcloud`.

Full command walkthrough (including Vercel wiring): root [README.md](../README.md#deploy-api-to-google-cloud-run).

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

Use the same value as local `ConnectionStrings__Postgres` (SSL required). Prefer Secret Manager.

Secret **name:** `pathdle-pg`  
Secret **value:** connection string only — do **not** include `ConnectionStrings__Postgres=`.

Never commit the string. Console: Secret Manager → Create, or:

```bash
# Replace with your real string locally; do not paste secrets into git/docs
printf '%s' 'YOUR_SUPABASE_CONNECTION_STRING' \
  | gcloud secrets create pathdle-pg --data-file=-

# Or update an existing secret:
# printf '%s' 'YOUR_SUPABASE_CONNECTION_STRING' | gcloud secrets versions add pathdle-pg --data-file=-
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

ASP.NET reads CORS as a **string array**. A single comma-separated `Cors__AllowedOrigins=...` value does **not** bind correctly — use indexed keys (`Cors__AllowedOrigins__0`, `__1`, …). Deploy scripts convert `CORS_ORIGINS` for you.

From the **monorepo root**:

**PowerShell**

```powershell
$env:GCP_PROJECT = "your-project-id"
$env:GCP_REGION = "us-central1"
$env:AR_REPO = "pathdle"
$env:CORS_ORIGINS = "https://www.example.com,https://example.com,http://localhost:3000"
$env:PG_SECRET = "pathdle-pg"
.\infra\cloud\deploy-api.ps1
```

**Bash**

```bash
export GCP_PROJECT=your-project-id
export GCP_REGION=us-central1
export AR_REPO=pathdle
export CORS_ORIGINS=https://www.example.com,https://example.com,http://localhost:3000
export PG_SECRET=pathdle-pg
chmod +x infra/cloud/deploy-api.sh
./infra/cloud/deploy-api.sh
```

Or manually (note `^|^` separator + indexed CORS):

```bash
gcloud builds submit --config infra/cloud/cloudbuild.api.yaml

gcloud run deploy pathdle-api \
  --region us-central1 \
  --image us-central1-docker.pkg.dev/$GCP_PROJECT/pathdle/pathdle-api:latest \
  --allow-unauthenticated \
  --port 8080 \
  --memory 512Mi \
  --min-instances 0 \
  --set-env-vars "^|^Pathdle__Storage=Postgres|ASPNETCORE_ENVIRONMENT=Production|Cors__AllowedOrigins__0=https://www.example.com|Cors__AllowedOrigins__1=http://localhost:3000" \
  --set-secrets "ConnectionStrings__Postgres=pathdle-pg:latest"
```

### Verify

```bash
curl.exe "$(gcloud run services describe pathdle-api --region us-central1 --format='value(status.url)')/api/health"
# expect: storage Postgres, database up
```

### Wire the web app

On Vercel (Production env), Root Directory = `apps/web`:

```
NEXT_PUBLIC_API_BASE_URL=https://pathdle-api-xxxxx-uc.a.run.app
```

Redeploy after setting the var. Local override: `apps/web/.env.local`.

If you use a `*.vercel.app` URL, add that exact origin to CORS and redeploy the API.

**Git push does not update Cloud Run** — only Vercel auto-deploys the web app unless you add CI.

## Local Docker smoke test (optional)

```bash
# From monorepo root
docker build -f apps/api/Dockerfile -t pathdle-api:local .

docker run --rm -p 8080:8080 \
  -e Pathdle__Storage=Postgres \
  -e ConnectionStrings__Postgres="Host=host.docker.internal;..." \
  -e Cors__AllowedOrigins__0=http://localhost:3000 \
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
  --set-env-vars "Pathdle__CorpusVersion=wiki-crawl-mvp-v1,Neo4j__Uri=YOUR_NEO4J_URI,Neo4j__Username=neo4j,Neo4j__Password=YOUR_NEO4J_PASSWORD,Neo4j__Database=neo4j" \
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
| `Cors__AllowedOrigins__N` | Cloud Run | Indexed frontend origins |
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
