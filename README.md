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

| App | Host | Status |
|-----|------|--------|
| `apps/web` | Vercel | Point `NEXT_PUBLIC_API_BASE_URL` at Cloud Run |
| `apps/api` | Google Cloud Run | Dockerfiles + scripts in `infra/cloud/` — see [docs/deploy-cloud-run.md](docs/deploy-cloud-run.md) |
| `apps/generator` | Cloud Run Job + Scheduler | Image ready; schedule later |
| Postgres | Supabase | Hosted play DB |
| Graph corpus | Neo4j Docker / Aura later | Generator only |
