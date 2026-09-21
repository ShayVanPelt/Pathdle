import type { Edge } from "@/lib/api/types";

/**
 * Nodes reachable from START through confirmed undirected links. Reveal hints do
 * not count: a player must successfully chart the link first.
 */
export function chartedNodeIds(startId: string, discoveredEdges: Edge[]) {
  const charted = new Set<string>([startId]);
  let changed = true;

  while (changed) {
    changed = false;
    for (const edge of discoveredEdges) {
      if (charted.has(edge.from) && !charted.has(edge.to)) {
        charted.add(edge.to);
        changed = true;
      } else if (charted.has(edge.to) && !charted.has(edge.from)) {
        charted.add(edge.from);
        changed = true;
      }
    }
  }

  return charted;
}

/** Canonical undirected edge key (matches API DailyPuzzle.UndirectedEdgeKey). */
export function undirectedEdgeKey(a: string, b: string) {
  return a <= b ? `${a}|${b}` : `${b}|${a}`;
}
