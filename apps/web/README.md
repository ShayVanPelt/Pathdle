# Pathdle Web (`apps/web`)

Next.js App Router frontend for the daily Wikipedia graph puzzle.

## Quick start

1. Start databases and API from repo root (see root [README.md](../../README.md)).
2. From this directory:

```bash
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Config

Set in monorepo root `.env` (not committed):

- `NEXT_PUBLIC_API_BASE_URL=http://localhost:5294`

## Scripts

| Command | Purpose |
|---------|---------|
| `npm run dev` | Dev server (Turbopack) |
| `npm run build` | Production build |
| `npm run start` | Run production build |
| `npm run lint` | ESLint |

## Product notes

- Full-bleed SVG constellation; top instrument HUD (brand | path rail | score) — no side panels.
- Labels inside node pills; circular display ring from API positions.
- Nodes only at start — edges appear when the player discovers them.
- Anonymous `player_key` in `localStorage` → `X-Player-Key` on game API calls.

See [AGENTS.md](./AGENTS.md) and root `.impeccable.md` for UI rules.
