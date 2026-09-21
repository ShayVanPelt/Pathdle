# Pathdle — Agent Guide

Daily shortest-path puzzle on a **group graph**: nodes share hidden properties/groups. Players discover undirected connections from START to TARGET with the lowest score.

## Monorepo

| Path | Role |
|------|------|
| `apps/web` | Next.js game UI (Vercel) |
| `apps/api` | ASP.NET Core play API (Cloud Run) |
| `apps/generator` | Wikidata group corpus + daily puzzle job (Cloud Run Job) |
| `infra/sql` | Postgres migrations + seeds |
| `docs/architecture.md` | Full architecture |

Read the package-specific `AGENTS.md` when working in that app.

## Non-negotiables

1. Gameplay never queries Neo4j — only Postgres in local dev; production JSON defaults to in-memory until wired.
2. Clients never receive the full edge set or optimal path until game complete.
3. Published daily puzzles are immutable (insert, never overwrite).
4. Anonymous-first: `X-Player-Key` header; optional auth later.
5. Do not build the entire product in one pass — incremental MVP.

## Scoring

Points are a cost (lower is better):

- Successful link drag: **+100** (permanent; relationship label shown; branching allowed)
- Failed link drag: **+200** (miss animation, no line)
- Reveal connections from a node: **+75**, max **3** paid hints per puzzle (dashed neighbor lines, no labels; free re-show; confirm clears hints from that node)
- No link removal / undo

See `docs/game-rules.md`, `Pathdle.Application.ScoringRules`, and web `SCORING`.

## Design Context

See `.impeccable.md` and `apps/web/AGENTS.md` before UI work.

**Visual product:** full-bleed night-void constellation; top instrument HUD (brand | path rail | score / hints); graph is the hero. Labels inside node pills; relationship plaques on confirmed edges; filled constellation (START/TARGET opposite, interior occupied).

**Anti-slop (always):** no Inter/Roboto/Arial as brand fonts; no purple-on-white / indigo gradients; no cyan-glow dark “AI dashboard”; no cream+terracotta newspaper look; no card soup; no emoji decoration; no gradient headline text; no bulky side panels trapping the board.

## Env

Root `.env.example` lists all variables. Never commit secrets. Never put Neo4j or service-role keys in `NEXT_PUBLIC_*`.
