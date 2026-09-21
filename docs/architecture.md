# Pathdle Architecture (MVP)

Daily shortest-path puzzle on a **Wikidata-derived group graph**. Players discover hidden undirected connections from START to TARGET on a shared daily board (~30–40 nodes).

## Stack

| Layer | Choice | Host |
|-------|--------|------|
| Frontend | Next.js, TypeScript, React, Tailwind | Vercel |
| API | ASP.NET Core, C# | Google Cloud Run |
| Puzzle generator | C# console / Cloud Run Job | Cloud Scheduler → Cloud Run Job |
| Play DB | PostgreSQL | Supabase |
| Graph corpus | Neo4j (Entity / Group / SHARES_GROUP) | Neo4j AuraDB / local Docker |

## Core principles

1. **Neo4j is for generation/analysis only.** Gameplay never queries Neo4j.
2. **Published puzzles are immutable.** New day = insert; never overwrite active puzzles.
3. **Clients never receive the full edge set or optimal path** until game complete (anti-cheat / spoiler).
4. **Anonymous-first identity.** Stable browser `player_key`; optional sign-in later to record scores.
5. **Groups are the foundation.** Controlled Wikidata property allowlist → co-membership edges with rarity; not Wikipedia hyperlinks.

## Runtime topology

```
Cloud Scheduler → Cloud Run Job (Pathdle.Generator)
                         ↓
                    Neo4j Aura (group corpus)
                         ↓
              publish snapshot → Supabase Postgres

Next.js (Vercel) ⇄ ASP.NET Core (Cloud Run) ⇄ Postgres
                         ↑
                    Neo4j not on this path
```

## Identity

- On first visit, create a UUID `player_key` in `localStorage`.
- Send on every game API call (`X-Player-Key` header).
- One game per `(daily_puzzle_id, player_key)`.

## Scoring (MVP)

Points are a **cost**. Lower score is better.

| Action | Points |
|--------|--------|
| Successful connection (shared group exists) | **+100** — permanent path line + relationship label |
| Failed attempt | **+200** — no line; miss animation |
| Reveal neighbors from one article | **+75** — dashed hints, no labels; max **3** paid; free re-show |

Constants: `Pathdle.Application.ScoringRules` and web `SCORING`.

## REST API

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/puzzles/today` | Public puzzle: nodes, layout, start/target (no edges) |
| `GET` | `/api/puzzles/{date}` | Archive |
| `POST` | `/api/games` | Start session |
| `GET` | `/api/games/{gameId}` | Resume |
| `POST` | `/api/games/{gameId}/attempts` | `{ fromId, toId }` |
| `POST` | `/api/games/{gameId}/reveals` | `{ articleId }` neighbor reveal |
| `POST` | `/api/games/{gameId}/complete` | Finish + results (includes optimal) |
| `GET` | `/api/health` | Probes |

Puzzle day is **UTC** calendar date. Edges are **undirected**.

## PostgreSQL

See `infra/sql/migrations/`.

- `daily_puzzles`: immutable board; server-only `edges` (`from`,`to`,`groupId`,`groupLabel`) / `optimal_path`.
- `player_games`: `discovered_edges`, `hint_edges`, `reveals`, `hints_used`, score, path.
- `leaderboard_entries`: optional later.

## Neo4j

```
(:Entity { id, title, popularity })
(:Group { id, label, property, valueId, frequency, rarity, memberCount })
(:Entity)-[:IN_GROUP]->(:Group)
(:Entity)-[:SHARES_GROUP { groupId, groupLabel, rarity }]->(:Entity)
(:CorpusMeta { version, entity_count, group_count, edge_count, built_at })
```

No player or daily-puzzle data in Neo4j.

## Daily generation

See [`docs/generator.md`](generator.md).

1. Seed = hash(`puzzle_date` + `corpus_version` + nonce).
2. Rarity-weighted walks → candidate paths; BFS validates length (~4–6).
3. Board ~30–40 nodes with mid-path traps; no shortcuts.
4. Idempotent publish by `puzzle_date`.

## Frontend UX (web)

- Full-bleed SVG constellation; top HUD (brand | path rail | score / links / hints).
- In-node pill labels; relationship plaques on confirmed edges; filled constellation spacing.
- Chart note for reveals; auto-open results on TARGET.

## Build order

| Step | Status | Notes |
|------|--------|-------|
| 1. Schema + migrations | ✅ | includes `hints_used` |
| 2. API + group-graph seed | ✅ | Dell → Vitamin C |
| 3. Game endpoints | ✅ | undirected + labels + hint budget |
| 4. SVG board UI | ✅ | edge labels, hints remaining |
| 5. Wire web ↔ API | ✅ | |
| 6. Neo4j group-graph ingest | ✅ | Wikidata + `--demo` |
| 7. Generator → Postgres | ✅ | rarity-weighted |
| 8. Scheduler + Cloud Run Job | 🔲 | |
| 9. Deploy | 🔲 | |
| 10. Leaderboard + auth | 🔲 | |

## Explicitly out of scope (for now)

- Full Wikidata on Aura
- Neo4j on the request path
- Auth UI / OAuth
- Overwriting published puzzles
- Compound groups (`genre ∩ decade`) — later if needed
- Heavy graph viz libraries / continuous force-directed physics
