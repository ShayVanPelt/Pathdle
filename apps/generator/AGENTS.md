# Pathdle Generator (`apps/generator`)

## Role

Cloud Run Job (later): Wikidata group-graph corpus ingest and daily puzzle publish.
Uses Neo4j for Entity/Group analysis; writes immutable rows to Postgres.

## Commands

```bash
# Neo4j must be up: docker compose up -d neo4j
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus --demo
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus --max-entities=2000
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --today
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --dry-run
```

`--demo` loads a hand-authored group graph (no Wikidata). Full ingest uses SPARQL + `wbgetentities` with `GroupVocabulary` allowlist/denylist and rarity.

Seeds: `apps/generator/data/seed_entities.txt` — curated multi-group hubs (companies, people, franchises, foods, cities). Not Wikipedia titles; not class QIDs. Expand with `--max-entities`.

Default `generate-daily` date = **tomorrow UTC**. Optimal length prefer **4–6** (small demo corpora may accept **3–5**). Board ~**30–40** nodes. Edges store `groupId` / `groupLabel`.

## Rules

- Never overwrite an existing `daily_puzzles` row for a date — idempotent skip.
- Ingest is separate from daily generation.
- Gameplay services must not depend on this process being online.

## Env

Root `.env`: `Neo4j__*`, `ConnectionStrings__Postgres`, `Pathdle__CorpusVersion` (e.g. `wikidata-groups-v1`).
