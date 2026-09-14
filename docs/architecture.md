# Pathdle Architecture (MVP)

Daily Wikipedia graph puzzle. Players discover a hidden directed graph from START to TARGET on a shared daily board (~60 nodes).

## Stack

| Layer | Choice | Host |
|-------|--------|------|
| Frontend | Next.js, TypeScript, React, Tailwind | Vercel |
| API | ASP.NET Core, C# | Google Cloud Run |
| Puzzle generator | C# console / Cloud Run Job | Cloud Scheduler → Cloud Run Job |
| Play DB | PostgreSQL | Supabase |
| Graph corpus | Neo4j | Neo4j AuraDB |

## Core principles

1. **Neo4j is for generation/analysis only.** Gameplay never queries Neo4j.
2. **Published puzzles are immutable.** New day = insert; never overwrite active puzzles.
3. **Clients never receive the full edge set or optimal path** until game complete (anti-cheat / spoiler).
4. **Anonymous-first identity.** Stable browser `player_key`; optional sign-in later to record scores.
5. **Wikipedia corpus is a bounded subset**, ingested separately from daily generation.

## Runtime topology

```
Cloud Scheduler → Cloud Run Job (Pathdle.Generator)
                         ↓
                    Neo4j Aura (corpus)
                         ↓
              publish snapshot → Supabase Postgres

Next.js (Vercel) ⇄ ASP.NET Core (Cloud Run) ⇄ Postgres
                         ↑
                    Neo4j not on this path
```

## Identity

### MVP

- On first visit, create a UUID `player_key` in `localStorage`.
- Send on every game API call (`X-Player-Key` header).
- One game per `(daily_puzzle_id, player_key)`.

### Later (opt-in)

- Supabase Auth (or equivalent).
- Link `player_key` → `user_id` without orphaning in-progress games.
- Public leaderboards only for signed-in users; guests keep full play.

## Scoring (MVP)

Points are a **cost**. Lower score is better.

| Action | Points |
|--------|--------|
| Successful directed link (drag A → B, edge exists) | **+100** — permanent path line |
| Failed link attempt (no edge that way) | **+100** — no line; miss animation |
| Reveal outbound neighbors from one article | **+75** — dashed **hint** lines only; player must still drag to confirm |

Constants live in `Pathdle.Application.ScoringRules` and `apps/web/src/lib/api/types.ts` (`SCORING`).

Failed attempts still cost points (exploration is not free).
Reveals are per article (once); reopening is free.

## REST API

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/puzzles/today` | Public puzzle: nodes, layout, start/target (no edges) |
| `GET` | `/api/puzzles/{date}` | Archive (optional) |
| `POST` | `/api/games` | Start session |
| `GET` | `/api/games/{gameId}` | Resume |
| `POST` | `/api/games/{gameId}/attempts` | `{ fromId, toId }` |
| `POST` | `/api/games/{gameId}/reveals` | `{ articleId }` outbound reveal (+75) |
| `POST` | `/api/games/{gameId}/complete` | Finish + results (includes optimal) |
| `GET` | `/api/health` | Probes |

Puzzle day is **UTC** calendar date.

## PostgreSQL

See `infra/sql/migrations/001_init.sql`.

- `daily_puzzles`: immutable published board + server-only `edges` / `optimal_path`.
- `player_games`: session progress keyed by `player_key`; nullable `user_id` for later auth.

## Neo4j

```
(:Article { id, title, page_id?, degree_out, degree_in, popularity? })
(:Article)-[:LINKS_TO]->(:Article)
(:CorpusMeta { version, article_count, edge_count, built_at })
```

No player or daily-puzzle data in Neo4j for MVP.

## Daily generation

1. Seed = hash(`puzzle_date` + `corpus_version`).
2. Sample START / TARGET with shortest-path length in band (e.g. 3–6).
3. Score difficulty (alt paths, dead ends, hubs, traps).
4. Build ~60-node board: path ∪ distractors.
5. Induce subgraph edges; store layout positions (not graph-aware clustering).
6. Idempotent publish: if `puzzle_date` exists, exit; never overwrite.

## Wikipedia corpus (MVP)

- Curated seeds + bounded crawl (tens of thousands of articles max).
- Versioned ingest (`corpus_version`); not rebuilt daily.
- Edges leaving the subset are dropped.

## Graph versioning

- `player_games.daily_puzzle_id` is the source of truth.
- Overnight generation inserts tomorrow’s row; in-progress games keep yesterday’s FK.
- Retain published puzzles at least 48–72h (preferably longer).

## Repo layout

```
apps/web         Next.js frontend
apps/api         ASP.NET Core play API
apps/generator   Daily puzzle + ingest job entrypoints
infra/sql        Supabase migrations
docs/            Architecture and design notes
```

## Frontend UX (web)

- Full-bleed SVG constellation on a night-atlas atmosphere; floating corner HUD (no side panels).
- Visual redesign must not change gameplay contracts (attempts, reveals, scoring, path).
- Design source of truth: `.impeccable.md` and `apps/web/AGENTS.md`.

## Build order

1. Schema + migrations
2. API skeleton + seeded frozen puzzle (no Neo4j)
3. Game endpoints (start / attempt / complete)
4. SVG board UI (pan, zoom, draw, floating HUD)
5. Wire web ↔ API
6. Neo4j ingest for small subset
7. Generator → Postgres publish
8. Scheduler + Cloud Run Job
9. Deploy
10. Reveals, leaderboard, auth opt-in

## Explicitly out of scope (for now)

- Full English Wikipedia on Aura
- Neo4j on the request path
- Auth UI / OAuth
- Overwriting published puzzles
- Heavy graph viz libraries / continuous force-directed physics
- Terraform / multi-env complexity
- Perfect difficulty ML
