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
- Local MVP storage may be in-memory seed; production target is Postgres/Supabase.
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
dotnet run --project apps/api/Pathdle.Api --launch-profile http
```

→ `http://localhost:5294`
