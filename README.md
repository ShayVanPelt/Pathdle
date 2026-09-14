# Pathdle

Daily Wikipedia graph puzzle. Discover a hidden path from START to TARGET on a full-bleed night-void constellation (top instrument HUD; graph is the hero).

## Status

Local MVP is runnable end-to-end:

- **Web** — Next.js UI on `localhost:3000`
- **API** — ASP.NET Core on `localhost:5294` (Development profile uses **Postgres**)
- **Play DB** — Docker Postgres (`docker compose up -d`)
- **Generator** — Neo4j corpus ingest + daily puzzle publish (`apps/generator`)
- **Graph corpus** — Docker Neo4j (`docker compose up -d`)

See [docs/architecture.md](docs/architecture.md), [docs/generator.md](docs/generator.md), and [docs/deploy-cloud-run.md](docs/deploy-cloud-run.md).

## Repo layout

```
apps/web              Next.js + TypeScript + Tailwind (Vercel)
apps/api              ASP.NET Core play API (Cloud Run)
apps/generator        Daily puzzle / ingest job (Cloud Run Job)
infra/sql/migrations  PostgreSQL schema
infra/sql/seed        Optional seed puzzle for first boot
docs/                 Architecture and design notes
Pathdle.sln           .NET solution
```

## Prerequisites

- Node.js 20+
- .NET 10 SDK
- Docker Desktop (Postgres + Neo4j for local dev)

## Local development

Use **three terminals** (or VS Code split terminals). Docker runs the databases only; API and web run directly.

### 1. Databases (once per session)

From repo root:

```bash
docker compose up -d
```

| Service | URL / port | Credentials |
|---------|------------|-------------|
| Postgres | `localhost:5432` | `pathdle` / `pathdle` / `pathdle` |
| Neo4j Browser | `http://localhost:7474` | `neo4j` / `pathdle-neo4j` |
| Neo4j Bolt | `bolt://localhost:7687` | same |

Schema + seed apply automatically on first Postgres container create.

### 2. API

```bash
dotnet run --project apps/api/Pathdle.Api --launch-profile http
```

- Base URL: `http://localhost:5294`
- Storage: `Pathdle:Storage=Postgres` in `appsettings.Development.json`
- Anonymous identity header: `X-Player-Key: <uuid>`

| Method | Path | Notes |
|--------|------|--------|
| `GET` | `/api/health` | |
| `GET` | `/api/puzzles/today` | Nodes + layout only (no edges) |
| `POST` | `/api/games` | Start / resume |
| `GET` | `/api/games/{id}` | Resume |
| `POST` | `/api/games/{id}/attempts` | `{ "fromId", "toId" }` |
| `POST` | `/api/games/{id}/reveals` | `{ "articleId" }` outbound hints |
| `POST` | `/api/games/{id}/complete` | Reveals optimal path |

Default seed puzzle (if no generated row for today): **Albert Einstein → Nintendo**, optimal length 3 via Physics → Mathematics.

### 3. Frontend

```bash
cd apps/web
npm install
npm run dev
```

Open `http://localhost:3000`. Uses monorepo root `.env` for `NEXT_PUBLIC_*` (see `.env.example`).

### 4. Generator (optional — corpus + daily puzzles)

Neo4j must be up. Copy root `.env.example` → `.env` and set `Neo4j__*` + `ConnectionStrings__Postgres` if needed.

```bash
# Rare: rebuild Wikipedia subset in Neo4j (local MVP often uses --max-articles=1200)
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus --max-articles=1200

# Publish tomorrow's puzzle (idempotent — skips if date exists)
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily

# Local testing: today's puzzle, no Postgres write
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --today --dry-run

# Sanity-check MediaWiki link fetch (e.g. Einstein → Physics)
dotnet run --project apps/generator/Pathdle.Generator -- diagnose-links --title=Albert_Einstein --expect=Physics
```

Full algorithm: [docs/generator.md](docs/generator.md).

## Env

Single monorepo `.env` at repo root (gitignored). See `.env.example` for:

- `ConnectionStrings__Postgres` — play DB (Docker or Supabase)
- `Neo4j__*` — generator corpus only
- `Pathdle__CorpusVersion` — corpus tag for generation
- `NEXT_PUBLIC_API_BASE_URL` — web → API (`http://localhost:5294` locally)

## Identity (MVP)

Anonymous browser `player_key` in `localStorage`, sent as `X-Player-Key`. Optional sign-in later.

## Deploy targets

| App | Host | Notes |
|-----|------|--------|
| `apps/web` | Vercel | Root Directory = `apps/web`; set `NEXT_PUBLIC_API_BASE_URL` |
| `apps/api` | Google Cloud Run | Commands below |
| `apps/generator` | Cloud Run Job (optional) | See [docs/deploy-cloud-run.md](docs/deploy-cloud-run.md) |
| Postgres | Supabase | Schema: `infra/sql/supabase_bootstrap.sql` |
| Graph corpus | Neo4j local / Aura later | Generator only |

**Git push does not redeploy the API.** Vercel auto-deploys the web app; Cloud Run needs a manual build/deploy (or a CI trigger you add later).

## Deploy API to Google Cloud Run

Prerequisites: [gcloud SDK](https://cloud.google.com/sdk/docs/install), billing-enabled GCP project, Supabase with schema applied, at least one `daily_puzzles` row for today.

Never put passwords or connection strings in git, README, or Cloud Run plain env vars — use Secret Manager.

### 1. One-time GCP setup

```bash
# Prefer project ID (string), not only the numeric project number
gcloud auth login
gcloud config set project YOUR_GCP_PROJECT_ID

gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  cloudbuild.googleapis.com \
  secretmanager.googleapis.com

gcloud artifacts repositories create pathdle \
  --repository-format=docker \
  --location=us-central1 \
  --description="Pathdle container images"
```

### 2. Postgres secret (Secret Manager)

Create secret **`pathdle-pg`** whose **value** is only the Supabase connection string  
(same as local `ConnectionStrings__Postgres` — SSL required).  
Do **not** prefix with `ConnectionStrings__Postgres=`.

- Console: [Secret Manager](https://console.cloud.google.com/security/secret-manager) → Create secret → name `pathdle-pg`
- Or CLI (paste your string yourself; do not commit it):

```bash
# Bash — pipe the connection string; do not echo it into chat/logs
printf '%s' 'YOUR_SUPABASE_CONNECTION_STRING' | gcloud secrets create pathdle-pg --data-file=-

# Update later:
# printf '%s' 'YOUR_SUPABASE_CONNECTION_STRING' | gcloud secrets versions add pathdle-pg --data-file=-
```

Grant Cloud Run’s default compute SA access:

```bash
PROJECT_NUMBER="$(gcloud projects describe "$(gcloud config get-value project)" --format='value(projectNumber)')"
RUNTIME_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"

gcloud secrets add-iam-policy-binding pathdle-pg \
  --member="serviceAccount:${RUNTIME_SA}" \
  --role="roles/secretmanager.secretAccessor"
```

**PowerShell equivalent:**

```powershell
$PROJECT_NUMBER = gcloud projects describe (gcloud config get-value project) --format="value(projectNumber)"
$RUNTIME_SA = "$PROJECT_NUMBER-compute@developer.gserviceaccount.com"

gcloud secrets add-iam-policy-binding pathdle-pg `
  --member="serviceAccount:$RUNTIME_SA" `
  --role="roles/secretmanager.secretAccessor"
```

### 3. Build + deploy API

From **monorepo root**. ASP.NET binds CORS as an array — use indexed env vars (`Cors__AllowedOrigins__0`, `__1`, …), not a single comma-separated string.

**PowerShell (Windows)**

```powershell
$Project = (gcloud config get-value project 2>$null).Trim()
$Region = "us-central1"
$Repo = "pathdle"
$Service = "pathdle-api"
$Image = "$Region-docker.pkg.dev/$Project/$Repo/$Service"
$Sha = (git rev-parse --short HEAD).Trim()

# Build image in Cloud Build
gcloud builds submit `
  --project $Project `
  --config infra/cloud/cloudbuild.api.yaml `
  --substitutions="_REGION=$Region,_REPOSITORY=$Repo,_SERVICE=$Service,SHORT_SHA=$Sha"

# Deploy — ^|^ uses | as env-var separator so URLs may contain commas if needed
# Replace origins with your real frontend URLs (custom domain + optional *.vercel.app)
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
  --set-env-vars "^|^Pathdle__Storage=Postgres|ASPNETCORE_ENVIRONMENT=Production|Cors__AllowedOrigins__0=https://www.example.com|Cors__AllowedOrigins__1=https://example.com|Cors__AllowedOrigins__2=http://localhost:3000" `
  --set-secrets "ConnectionStrings__Postgres=pathdle-pg:latest"
```

**Bash**

```bash
GCP_PROJECT="$(gcloud config get-value project)"
GCP_REGION=us-central1
AR_REPO=pathdle
SERVICE=pathdle-api
IMAGE="${GCP_REGION}-docker.pkg.dev/${GCP_PROJECT}/${AR_REPO}/${SERVICE}"

gcloud builds submit \
  --project "${GCP_PROJECT}" \
  --config infra/cloud/cloudbuild.api.yaml \
  --substitutions="_REGION=${GCP_REGION},_REPOSITORY=${AR_REPO},_SERVICE=${SERVICE},SHORT_SHA=$(git rev-parse --short HEAD)"

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
  --set-env-vars "^|^Pathdle__Storage=Postgres|ASPNETCORE_ENVIRONMENT=Production|Cors__AllowedOrigins__0=https://www.example.com|Cors__AllowedOrigins__1=https://example.com|Cors__AllowedOrigins__2=http://localhost:3000" \
  --set-secrets "ConnectionStrings__Postgres=pathdle-pg:latest"
```

Helpers (same idea): `infra/cloud/deploy-api.ps1` / `deploy-api.sh` — see [docs/deploy-cloud-run.md](docs/deploy-cloud-run.md).

### 4. Verify

```bash
# PowerShell: prefer curl.exe to avoid Invoke-WebRequest prompts
API_URL="$(gcloud run services describe pathdle-api --region us-central1 --format='value(status.url)')"
curl.exe "${API_URL}/api/health"
# expect: {"status":"ok",...,"storage":"Postgres","database":"up"}

curl.exe "${API_URL}/api/puzzles/today"
```

### 5. Wire Vercel

1. Import the GitHub repo; **Root Directory** = `apps/web`
2. Env var (Production + Preview):

   `NEXT_PUBLIC_API_BASE_URL=<Cloud Run service URL>`  
   (no trailing slash; not a secret — `NEXT_PUBLIC_` is intentional)

3. Redeploy after setting the var (must rebuild)
4. Custom domain: Project → Domains → add your domain; point DNS at Vercel
5. If you use a `*.vercel.app` URL, add that exact origin as another `Cors__AllowedOrigins__N` and redeploy the API

### Production env checklist

| Name | Where | Notes |
|------|--------|--------|
| `Pathdle__Storage=Postgres` | Cloud Run env | Required (JSON default is InMemory) |
| `ConnectionStrings__Postgres` | Secret Manager → Cloud Run secret mount | Never commit |
| `Cors__AllowedOrigins__0` … | Cloud Run env | Indexed list of frontend origins |
| `NEXT_PUBLIC_API_BASE_URL` | Vercel | Cloud Run HTTPS URL |

### Redeploy after API code changes

```bash
# From repo root — rebuild + deploy again (same commands as §3)
gcloud builds submit --config infra/cloud/cloudbuild.api.yaml ...
gcloud run deploy pathdle-api ...
```

More detail (generator job, local Docker smoke test): [docs/deploy-cloud-run.md](docs/deploy-cloud-run.md).
