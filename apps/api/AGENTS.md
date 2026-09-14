# Pathdle API (`apps/api`)

## Stack

ASP.NET Core minimal APIs, C#. Projects: `Pathdle.Api`, `Pathdle.Application`, `Pathdle.Infrastructure`.

## Rules

- Public puzzle DTOs: nodes + layout + start/target only — **strip `edges` and `optimal_path`**.
- Validate connection attempts server-side against the published edge set.
- Reveal endpoint returns **outbound hint edges only** (not path discoveries). Player must still drag to confirm.
- Scoring: see `ScoringRules` (+100 link attempt, +75 reveal hint). Lower score is better.
- Require `X-Player-Key` for game mutations and reads of player state.
- Puzzle day = UTC `DateOnly`.
- **Local dev** uses Postgres (`Pathdle:Storage=Postgres` in `appsettings.Development.json` + Docker). Production default JSON is `InMemory` until Cloud Run gets `ConnectionStrings__Postgres`.
- Do not call Neo4j from request handlers.

## Endpoints

| Method | Path |
|--------|------|
| `GET` | `/` index |
| `GET` | `/api/health` |
| `GET` | `/api/puzzles/today` |
| `POST` | `/api/games` |
| `GET` | `/api/games/{id}` |
| `POST` | `/api/games/{id}/attempts` |
| `POST` | `/api/games/{id}/reveals` |
| `POST` | `/api/games/{id}/complete` |

## Local

```bash
# 1) Copy root env and set Supabase (or Docker) connection
cp .env.example .env
# edit .env → ConnectionStrings__Postgres password

# 2) Local Postgres (recommended for dev)
docker compose up -d postgres

# 3) API
dotnet run --project apps/api/Pathdle.Api --launch-profile http
```

→ `http://localhost:5294`

One monorepo `.env` at the repo root (gitignored). Do not put secrets in `apps/api/Pathdle.Api/.env`.
