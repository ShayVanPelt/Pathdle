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
- **Click node** (no drag) — open chart note → Reveal outbound hints
- **Drag background** — pan
- **Scroll / pinch / + −** — zoom; **recenter** fits START, TARGET, and path
- Chart rail hops — select / focus a star on the path

## Presentation (UI)

Full-bleed night-void constellation. Top instrument row: brand | charted path | score/links. Labels sit inside node pills; display layout uses a circular ring (API coords are direction only). Edge styles: solid amber path, muted branch, dashed teal hints. Reaching TARGET auto-opens results. See `.impeccable.md` and `apps/web/AGENTS.md` — gameplay above is unchanged by chrome.

## Privacy / anti-spoiler

- Clients never download the full edge list up front.
- Reveals are paid, per-article, server-validated hints only.
- Optimal path is only returned on complete.
