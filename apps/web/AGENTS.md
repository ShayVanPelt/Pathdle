<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

# Pathdle Web (`apps/web`)

## Stack

Next.js App Router, TypeScript, React, Tailwind CSS. Deploy: Vercel.

## Product UI rules

- Main experience = **full-bleed** interactive SVG constellation (~60 nodes later; seed has ~16). Atmosphere fills the viewport; the graph is the hero.
- HUD floats in corners (brand, score/links, path, hint) — **no** bulky side panels or permanent “controls & costs” chrome.
- Initial board: nodes only, **no** visible edges until the player discovers them.
- Layout positions come from the API — do not rearrange by graph topology. Unconnected nodes may drift ~1–2px (CSS); connected nodes settle.
- Pan, zoom, click nodes, drag to attempt connections.
- **Click** a node → menu to reveal outbound **hints** (+75). Hints are a different color and are not path links until the player drags to confirm.
- **Drag** node → node → test/confirm link (+100). Multiple links from the same node (including earlier nodes) are allowed; confirmed links are never removed.
- Score only grows for new attempts/reveals — no undo. Animate score changes in the HUD.
- START / TARGET: compact glowing markers (not huge discs), distinct colors, labeled.
- Anonymous `player_key` in `localStorage` → send `X-Player-Key` on game calls.
- Never trust or display full edge lists from the client side as “truth.”
- Scoring constants: keep web `SCORING` in sync with API `ScoringRules`.

## Design (non–vibe-coded)

Follow root `.impeccable.md`.

Quick checklist before shipping UI:

1. One strong visual metaphor (night atlas / knowledge constellation) — not “default shadcn dashboard”
2. Custom font pairing; avoid Inter/Roboto/Arial as the brand voice
3. Tinted neutrals + amber/teal accents; soft node glow OK — no neon spam / cyan-on-black dashboard
4. No card grids wrapping the board; HUD is overlay, board is full-bleed
5. Motion is restrained (float, edge draw, fail fade, score bump) — not ambient particle spam
6. Brand name readable on first paint without crushing the board

## Component map

| Area | Primary files |
|------|----------------|
| Boot / game state | `src/components/game/PathdleGame.tsx` |
| Camera + interaction | `GameBoard.tsx` |
| Atmosphere | `FieldAtmosphere.tsx` |
| Nodes / edges | `GraphNode.tsx`, `GraphEdges.tsx` |
| HUD / score | `GameHud.tsx` |
| Help / results | `HowToPlay.tsx`, `ResultsPanel.tsx` |
| Tokens / motion | `src/app/globals.css` |

## API

`NEXT_PUBLIC_API_BASE_URL` (see `.env.example`). Local default `http://localhost:5294`.
