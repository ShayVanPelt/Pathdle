import type { Edge } from "@/lib/api/types";

/**
 * Nodes reachable from START through confirmed directed links. Reveal hints do
 * not count: a player must successfully chart the link first.
 */
export function chartedNodeIds(startId: string, discoveredEdges: Edge[]) {
  const charted = new Set<string>([startId]);
  let changed = true;

  while (changed) {
    changed = false;
    for (const edge of discoveredEdges) {
      if (!charted.has(edge.from) || charted.has(edge.to)) continue;
      charted.add(edge.to);
      changed = true;
    }
  }

  return charted;
}
