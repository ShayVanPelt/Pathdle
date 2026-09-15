# Pathdle daily puzzle generator

How we build a challenging, fair daily board from a Neo4j Wikipedia corpus.

## Commands

```bash
# From repo root (loads root .env). Neo4j must be up: docker compose up -d neo4j
dotnet run --project apps/generator/Pathdle.Generator -- ingest-corpus [--max-articles=8000] [--seeds=path]
dotnet run --project apps/generator/Pathdle.Generator -- generate-daily [--date=YYYY-MM-DD] [--today] [--dry-run]
dotnet run --project apps/generator/Pathdle.Generator -- diagnose-links [--title=Albert_Einstein] [--expect=Physics]
```

- `ingest-corpus` — rare; bounded MediaWiki crawl → Neo4j. Local MVP often uses `--max-articles=1200`; default cap is 8000. Override seeds with `--seeds=apps/generator/data/seed_articles.txt`.
- `generate-daily` — once per day; default date = **tomorrow UTC** (`--today` for local testing). `--dry-run` builds a board but does not write Postgres.
- `diagnose-links` — fetch outbound links for one title; useful to verify MediaWiki pagination (e.g. Einstein → Physics on ~page 2).
- Publish is **idempotent**: existing `daily_puzzles.puzzle_date` → skip (never overwrite).

## Design goals

| Goal | Rule |
|------|------|
| Optimal length | Shortest directed START→TARGET hops ∈ **{4, 5, 6}** |
| Red herrings | Mid-path nodes get **trap branches** (attractive wrong outs) |
| Board size | About **50–70** nodes |
| Fairness | One clear optimal spine; traps look promising but are not shortest |

## Path selection

1. Seed RNG with `hash(puzzle_date + corpus_version + run_nonce)` — each generate run gets a fresh nonce; `--date=` pins the calendar day for scheduled jobs.
2. Without `--date=`, pick the earliest free `puzzle_date` on or after the floor (`--today` → today UTC, default → tomorrow UTC).
2. Candidate START: mid out-degree / mid in-degree (not mega-hubs, not stubs).
3. BFS from START; collect nodes at distance 4, 5, or 6 whose **TARGET** sits in a mid in-degree band (about 15–400 in, ≥2 out) so leafy sinks are excluded; gather up to ~24 valid paths and RNG-pick one (not first-hit).
4. Accept only if:
   - shortest length ∈ [4, 6]
   - mid-path nodes have outbound branches usable as traps
   - path is not dominated by extreme hubs
   - among collected candidates, prefer lower alternate-shortest-path counts (mid-degree TARGETs are denser than leaf sinks)
5. On reject, resample (hard attempt cap).

## Red herrings (mid-path traps)

For each optimal-path node at hop `1 .. length-2` (exclude START and TARGET):

1. Take real outbound neighbors that are **not** on any shortest START→TARGET path.
2. Prefer “attractive” neighbors (higher in-degree / topical hubs).
3. Expand a small trap subgraph (depth 1–2, hard node budget).
4. Cap total board size so the constellation stays readable.

Also add a few thematic distractors near START/TARGET so the early board is not empty.

## Difficulty jsonb

Stored on `daily_puzzles.difficulty` (no ML):

```json
{
  "optimal_length": 5,
  "alt_shortest_count": 1,
  "mid_path_branch_avg": 3.2,
  "trap_node_count": 28,
  "trap_edge_count": 41,
  "hub_penalty": 0.15,
  "score": 62,
  "band": "medium-hard"
}
```

Composite `score` must land in the accepted band (roughly medium–hard). Outside band → reject and resample.

## Layout

Positions in `[0,1]²`, not runtime force-directed:

- START left, TARGET right
- Optimal path roughly left → right
- Trap nodes orbit their parent with jitter

## Corpus (Neo4j)

Ingest builds an **induced subgraph** from curated seeds (`apps/generator/data/seed_articles.txt`) plus high-overlap 1-hop expansions.

Defaults favor fidelity over speed: scout ≈6×500 pages/seed, induce unlimited keep-filtered pagination (`--induce-pages=0`), retry **all** empty outbound fetches, then print a canary edge report (`OK` / `MISS` / `SKIP`). A `MISS` means both endpoints were kept but the Wikipedia-class link never landed in Neo4j.

Pipeline:

1. **Scout** — fetch a few alphabetical `prop=links` pages per seed (enough to catch mid-alphabet neighbors like Einstein→Physics on ~page 2) and rank expansion candidates by multi-seed overlap.
2. **Induce** — for every kept article, paginate outbound links (with alphabetical early-stop past the last keep title) and retain only keep→keep edges.
3. **Retry** — serially re-fetch articles that came back empty (rate limits / transient MediaWiki failures).

Important: link fetches are **per-title isolated** (one failure must not discard a whole batch). Requests are single-flight and back off on HTTP 429/403.

```
(:Article { id, title, page_id, degree_out, degree_in, popularity })
(:Article)-[:LINKS_TO]->(:Article)
(:CorpusMeta { version, article_count, edge_count, built_at })
```

- Curated seeds + bounded BFS via MediaWiki API.
- Edges leaving the subset are dropped.
- Local Docker: `bolt://localhost:7687` (see `docker-compose.yml`).
- Aura: same code; change `Neo4j__*` in `.env`.

## Reject / accept checklist

Accept a candidate when all hold:

- [ ] `optimal_length` ∈ [4, 6]
- [ ] board nodes ∈ [45, 75] (soft; hard clamp with fill/trim)
- [ ] ≥ 1 trap branch off mid-path
- [ ] difficulty `score` in configured band
- [ ] START ≠ TARGET; path reconstructible in induced board edges
