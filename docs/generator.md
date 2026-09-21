# Pathdle Generator

Daily puzzle publish + Wikidata group-graph corpus ingest.

## Model

```
(:Entity)-[:IN_GROUP]->(:Group)
(:Entity)-[:SHARES_GROUP {groupId, groupLabel, rarity}]->(:Entity)
```

Playable edges are **undirected** co-memberships under a controlled Wikidata property allowlist (`GroupVocabulary`). Mega-hubs (United States, human, company, …) are denylisted. Groups outside `4 ≤ |G| ≤ 400` are dropped. Rarity = `(1/frequency)^0.5`.

## Commands

```bash
# Neo4j must be up: docker compose up -d neo4j

# Offline demo corpus (Dell→Vitamin C style) — no Wikidata network needed
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus --demo

# Wikidata ingest (bounded SPARQL + wbgetentities)
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus --max-entities=2000

dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --today
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily --dry-run
```

Default `generate-daily` date = **tomorrow UTC**, advancing to the next free `puzzle_date`. Use `--today` for local testing. Each run uses a unique 12-char seed nonce (`seed=…`).

## Generation pipeline

1. Weighted walks favoring high-rarity `SHARES_GROUP` edges
2. BFS validates unweighted shortest length (prefer **4–6**; small demo corpora may accept **3–5**)
3. Board ~**30–40** nodes: optimal path ∪ mid-path traps ∪ light distractors (never shorten optimal)
4. Layout positions in `[0,1]²`; web scatters into a filled constellation for display
5. Publish immutable Postgres row (`groupId` / `groupLabel` on edges)

## Quality checklist

- Optimal length in band; no accidental shortcuts on the induced board
- Mid-path branching + traps present
- Interesting avg rarity (not all mega-hubs)
- Idempotent publish: existing `puzzle_date` → skip

## Seeds

`apps/generator/data/seed_entities.txt` — curated Wikidata **QIDs** (companies, people, franchises, foods, cities). Prefer multi-group bridge hubs. Do not seed class items (film/country/company types); SPARQL expands those via allowlisted `P31` classes. Country mega-hubs are denylisted as group values — cities are better place anchors.
