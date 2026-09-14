# Pathdle Generator (`apps/generator`)

## Role

Cloud Run Job: Wikipedia corpus ingest (rare) and daily puzzle publish (once/day). Uses Neo4j for graph analysis; writes immutable rows to Postgres.

## Rules

- Never overwrite an existing `daily_puzzles` row for a date — idempotent skip.
- Ingest is separate from daily generation; do not rebuild Wikipedia daily.
- Gameplay services must not depend on this process being online.

## Status

Scaffold only — commands planned: `ingest-corpus`, `generate-daily`.
