# Pathdle Generator (`apps/generator`)

## Role

Cloud Run Job (later): Wikipedia corpus ingest (rare) and daily puzzle publish (once/day).
Uses Neo4j for graph analysis; writes immutable rows to Postgres.

## Commands

```bash
# Neo4j must be up: docker compose up -d neo4j
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus --max-articles=8000
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --today
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --dry-run
dotnet run --project apps/generator/Pathdle.Generator -- diagnose-links --title=Albert_Einstein --expect=Physics
```

Default `generate-daily` date = **tomorrow UTC**. Use `--today` for local testing. Local corpus ingest often uses `--max-articles=1200` (full cap 8000).

Cloud Run Job image: `apps/generator/Dockerfile` — see `docs/deploy-cloud-run.md`.

## Rules

- Never overwrite an existing `daily_puzzles` row for a date — idempotent skip.
- Ingest is separate from daily generation; do not rebuild Wikipedia daily.
- Optimal length must be 4–6; mid-path trap red herrings required — see `docs/generator.md`.
- Gameplay services must not depend on this process being online.

## Env

Root `.env`: `Neo4j__*`, `ConnectionStrings__Postgres`, `Pathdle__CorpusVersion`.
