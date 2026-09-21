# Pathdle API (`apps/api`)

## Stack

ASP.NET Core minimal APIs, C#. Projects: `Pathdle.Api`, `Pathdle.Application`, `Pathdle.Infrastructure`.

## Rules

- Public puzzle DTOs: nodes + layout + start/target only — **strip `edges` and `optimal_path`**.
- Edges are **undirected** shared-group links; validate attempts server-side against the published edge set.
- On success, return `groupId` / `groupLabel`; discovered edges include labels; hints never do.
- Reveal returns **all neighbors** as unlabeled hint edges. Paid once per article; max **3** paid hints (`hints_used`). Re-show is free.
- Scoring: +100 success, +200 miss, +75 paid hint. Lower is better.
- Require `X-Player-Key` for game mutations and reads of player state.
- Puzzle day = UTC `DateOnly`.
- **Local / Cloud Run** use Postgres (`Pathdle:Storage=Postgres`). Production JSON defaults to `InMemory` until Cloud Run sets `Pathdle__Storage=Postgres` + `ConnectionStrings__Postgres`.
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
cp .env.example .env
# edit ConnectionStrings__Postgres
docker compose up -d postgres   # optional
dotnet run --project apps/api/Pathdle.Api --launch-profile http
```

→ `http://localhost:5294`

## Cloud Run

See `docs/deploy-cloud-run.md`. Container listens on **8080**.
