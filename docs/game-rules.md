# Pathdle game rules

Source of truth for costs: `apps/api/Pathdle.Application/ScoringRules.cs`  
Mirrored in the web client: `apps/web/src/lib/api/types.ts` → `SCORING`

## Objective

Connect **START** to **TARGET** by discovering undirected relationships between nodes (shared groups/properties). Finish with the **lowest score**. Compare your route to the stored optimal path at the end.

## Scoring

**Lower score is better.** Points are penalties.

| Action | Cost | Effect |
|--------|------|--------|
| Drag A → B when a shared-group edge exists | **+100** | Permanent path link; relationship label appears on the line |
| Drag A → B when no edge exists | **+200** | No line; miss animation |
| Reveal connections from one charted node | **+75** | Dashed teal hints to **all** neighbors (no relationship labels). Max **3** paid reveals per puzzle. |

Already-confirmed links do not charge again. Re-showing hints for a previously revealed node is **free**. Confirming a link from a node clears its outbound dashed hints.

## Hints vs path links

- **Hint edges** (`hintEdges`): visual only (dashed teal). No group labels. Not on your path until you **drag to confirm**.
- **Discovered edges** (`discoveredEdges`): permanent links with `groupLabel`. Never removed.
- You may draw **multiple links from the same node** (branching from any charted node).
- Score only increases for new successful links, misses, or paid hints — no undo.

## Controls

- **Drag node → node** — test / confirm a connection
- **Click node** (no drag) — chart note → Reveal connections
- **Drag background** — pan
- **Scroll / pinch / + −** — zoom; **recenter** fits START, TARGET, and path

## Privacy / anti-spoiler

- Clients never download the full edge list up front.
- Hints are paid (budgeted), per-article, server-validated neighbor lists only — no relationship spoilers on hints.
- Optimal path is only returned on complete.
- Relationship labels appear only after a successful confirm.
