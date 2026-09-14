# Pathdle

Daily Wikipedia graph puzzle. Discover a hidden path from START to TARGET on a full-bleed night-atlas constellation (floating HUD; graph is the hero).

## Status

Play API MVP is runnable with an **in-memory seeded puzzle** (no Neo4j / Postgres required yet).

See [docs/architecture.md](docs/architecture.md).

## Repo layout

```
apps/web              Next.js + TypeScript + Tailwind (Vercel)
apps/api              ASP.NET Core play API (Cloud Run)
apps/generator        Daily puzzle / ingest job (Cloud Run Job)
infra/sql/migrations  Supabase PostgreSQL schema
infra/sql/seed        Optional Postgres seed of the frozen puzzle
docs/                 Architecture notes
Pathdle.sln           .NET solution
```

## Prerequisites

- Node.js 20+
- .NET 10 SDK
- Supabase project (later)
- Neo4j AuraDB (later, for generation only)

## Local development

### Postgres (play DB)

```bash
docker compose up -d
```

- Host: `localhost:5432`
- DB / user / password: `pathdle` / `pathdle` / `pathdle`
- Schema + seed apply automatically on first container create
- API Development profile uses `Pathdle:Storage=Postgres`

### API (playable now)

```bash
dotnet run --project apps/api/Pathdle.Api --launch-profile http
```

Base URL: `http://localhost:5294`

Send anonymous identity as header: `X-Player-Key: <uuid>`

| Method | Path | Notes |
|--------|------|--------|
| `GET` | `/api/health` | |
| `GET` | `/api/puzzles/today` | Nodes + layout only (no edges) |
| `POST` | `/api/games` | Start / resume |
| `GET` | `/api/games/{id}` | Resume |
| `POST` | `/api/games/{id}/attempts` | `{ "fromId", "toId" }` |
| `POST` | `/api/games/{id}/complete` | Reveals optimal path |

Seed puzzle: **Albert Einstein → Nintendo**, optimal length 3 via Physics → Mathematics.

### Frontend

```bash
cd apps/web
npm install
npm run dev
```

Uses monorepo root `.env` for `NEXT_PUBLIC_*` (see `.env.example`).

### Generator

```bash
dotnet run --project apps/generator/Pathdle.Generator
```

### Database (later)

Apply `infra/sql/migrations/001_init.sql`, optional `infra/sql/seed/001_seed_puzzle.sql`.
The API still uses in-memory storage until Postgres is wired.

## Identity (MVP)

Anonymous browser `player_key` in `localStorage`, sent as `X-Player-Key`. Optional sign-in later.

## Deploy targets (planned)

| App | Host |
|-----|------|
| `apps/web` | Vercel |
| `apps/api` | Google Cloud Run |
| `apps/generator` | Cloud Run Job + Cloud Scheduler |
| Postgres | Supabase |
| Graph corpus | Neo4j AuraDB |
