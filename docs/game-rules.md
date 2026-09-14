# Pathdle game rules (MVP)

Source of truth for costs: `apps/api/Pathdle.Application/ScoringRules.cs`  
Mirrored in the web client: `apps/web/src/lib/api/types.ts` → `SCORING`

## Objective

Reach **TARGET** from **START** by discovering directed links. Compare your path to the stored optimal path at the end.

## Scoring

**Lower score is better.** Points are penalties.

| Action | Cost | Effect |
|--------|------|--------|
| Drag A → B when a directed board edge exists | **+100** | Permanent amber path link (kept forever; branching allowed) |
| Drag A → B when no edge exists | **+100** | No line; miss animation (+100 label) |
| Click article → **Reveal hints** | **+75** | Dashed teal hints only. Still must drag to confirm. Hints are not removed when you confirm. |

Already-confirmed links and already-revealed articles do not charge again.

## Hints vs path links

- **Hint edges** (`hintEdges`): visual only (dashed teal). Not on your path until you **drag to confirm**.
- **Discovered edges** (`discoveredEdges`): permanent links from successful drags. Never removed.
- You may draw **multiple links from the same node**, including from nodes earlier in your chart (branching).
- Score only increases when you make a **new** drag attempt (success or fail) or a new reveal — there is no undo/remove.

## Controls

- **Drag node → node** — test / confirm a link (draw-on success / brief miss on fail)
- **Click node** (no drag) — open reveal-hints menu
- **Drag background** — pan
- **Scroll** — zoom

## Presentation (UI)

The playfield is a full-bleed night-atlas constellation. HUD floats at the corners (brand, score/links, path). See `.impeccable.md` and `apps/web/AGENTS.md` for visual rules — gameplay above is unchanged by chrome.

## Privacy / anti-spoiler

- Clients never download the full edge list up front.
- Reveals are paid, per-article, server-validated hints only.
- Optimal path is only returned on complete.
