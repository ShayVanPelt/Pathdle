# Pathdle — Agent Guide

Daily Wikipedia graph puzzle. Players discover a hidden directed graph from START to TARGET.

## Monorepo

| Path | Role |
|------|------|
| `apps/web` | Next.js game UI (Vercel) |
| `apps/api` | ASP.NET Core play API (Cloud Run) |
| `apps/generator` | Daily puzzle / ingest job (Cloud Run Job) |
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

- Successful link drag: **+100** (permanent; branching from any charted node is allowed)
- Failed link drag: **+100** (miss animation, no line)
- Reveal outbound from a node (click → menu): **+75** (dashed hints only — not permanent path links; confirming a link does not remove the hint record)
- No link removal / undo — score only increases for new actions

See `docs/game-rules.md`, `Pathdle.Application.ScoringRules`, and web `SCORING`.

## Design Context

See `.impeccable.md` and `apps/web/AGENTS.md` before UI work.

**Visual product:** full-bleed knowledge constellation; floating corner HUD; graph is the hero.

**Anti-slop (always):** no Inter/Roboto/Arial as brand fonts; no purple-on-white / indigo gradients; no cyan-glow dark “AI dashboard”; no cream+terracotta newspaper look; no card soup; no emoji decoration; no gradient headline text; no bulky side panels trapping the board.

## Env

Root `.env.example` lists all variables. Never commit secrets. Never put Neo4j or service-role keys in `NEXT_PUBLIC_*`.
