<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

# Pathdle Web (`apps/web`)

## Stack

Next.js App Router, TypeScript, React, Tailwind CSS. Deploy: Vercel.

## Product UI rules

- Main experience = **full-bleed** interactive SVG constellation (~60 nodes later). Atmosphere fills the viewport; the graph is the hero.
- **Top instrument HUD** (one row, equal plate height): Pathdle + How to play | charted path rail | Score / Links. No permanent side panels.
- Initial board: nodes only, **no** visible edges until the player discovers them.
- API layout positions are a **direction hint only**. Display spreads nodes into a circular ring with pill collision avoidance (`spreadDisplayPositions` in `src/lib/graph/layout.ts`) — do not invent a second topology layout from edges.
- Labels live **inside** node pills. Hover/selection highlights the pill only — never spawn external label chips.
- Pan, zoom (`+`/`−`/wheel/pinch), recenter, click nodes, drag to attempt connections.
- **Click** a node → chart note (reveal outbound hints +75). Hints are dashed teal and are not path links until the player drags to confirm. Confirming a link from that node clears its remaining outbound hints.
- **Drag** node → node → test/confirm link (+100). Branching from any charted node is allowed; confirmed links are never removed.
- Score only grows for new attempts/reveals — no undo. Animate score changes in the HUD.
- Reaching TARGET auto-opens results (breakdown + share); player may keep exploring the board.
- Anonymous `player_key` in `localStorage` → send `X-Player-Key` on game calls.
- Never trust or display full edge lists from the client side as “truth.”
- Scoring constants: keep web `SCORING` in sync with API `ScoringRules`.

## Design (non–vibe-coded)

Follow root `.impeccable.md`.

Quick checklist before shipping UI:

1. One strong visual metaphor (night atlas / knowledge constellation) — not “default shadcn dashboard”
2. Custom font pairing; avoid Inter/Roboto/Arial as the brand voice
3. Near-black void + amber/teal accents; soft glows OK — no neon spam / cyan-on-black dashboard
4. No card grids wrapping the board; HUD is overlay instruments, board is full-bleed
5. Motion is restrained (edge draw, fail fade, score bump) — not ambient particle spam
6. Brand name readable on first paint without crushing the board
7. In-node labels + circular spacing — no overlapping floating chips

## Component map

| Area | Primary files |
|------|----------------|
| Boot / game state | `src/components/game/PathdleGame.tsx` |
| Camera + interaction | `GameBoard.tsx` |
| Atmosphere / stars | `FieldAtmosphere.tsx` |
| Nodes / edges | `GraphNode.tsx`, `GraphEdges.tsx` |
| Display layout | `src/lib/graph/layout.ts` |
| Top HUD / path rail | `GameHud.tsx`, `ChartRail.tsx` |
| Chart note | `ChartNote.tsx` |
| Camera buttons | `CameraControls.tsx` |
| Help / results | `HowToPlay.tsx`, `ResultsPanel.tsx` |
| Tokens / motion | `src/app/globals.css` |

## API

`NEXT_PUBLIC_API_BASE_URL` (see `.env.example`). Local default `http://localhost:5294`.
