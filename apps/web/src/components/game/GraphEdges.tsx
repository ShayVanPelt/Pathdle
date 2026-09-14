"use client";

import type { Edge } from "@/lib/api/types";
import { edgeCurvePath, type Point, type Rect } from "@/lib/graph/layout";

type GhostLine = {
  from: Point;
  to: Point;
  kind: "fail" | "pending";
  key: number;
  label?: string;
};

type Props = {
  positions: Map<string, Point>;
  obstacles: Rect[];
  discoveredEdges: Edge[];
  visibleHints: Edge[];
  pathEdgeKeys: Set<string>;
  ghost: GhostLine | null;
  dragLine: { from: Point; to: Point; nearValid: boolean } | null;
  focusId: string | null;
  focusNeighborIds: Set<string>;
};

function edgeDim(
  from: string,
  to: string,
  focusId: string | null,
  neighbors: Set<string>,
  onPath: boolean,
) {
  if (!focusId) return "";
  const involved =
    from === focusId ||
    to === focusId ||
    (neighbors.has(from) && neighbors.has(to));
  if (involved) return "";
  if (onPath) return " is-soft";
  return " is-dimmed";
}

export function GraphEdges({
  positions,
  obstacles,
  discoveredEdges,
  visibleHints,
  pathEdgeKeys,
  ghost,
  dragLine,
  focusId,
  focusNeighborIds,
}: Props) {
  return (
    <g pointerEvents="none">
      {visibleHints.map((edge) => {
        const a = positions.get(edge.from);
        const b = positions.get(edge.to);
        if (!a || !b) return null;
        const d = edgeCurvePath(a, b, obstacles);
        return (
          <g
            key={`hint-${edge.from}->${edge.to}`}
            className={`pathdle-edge-layer${edgeDim(edge.from, edge.to, focusId, focusNeighborIds, false)}`}
          >
            <path
              d={d}
              fill="none"
              className="pathdle-hint-edge stroke-[var(--hint)]"
              strokeWidth={2.25}
              strokeDasharray="7 9"
              strokeLinecap="round"
            />
          </g>
        );
      })}

      {discoveredEdges.map((edge) => {
        const a = positions.get(edge.from);
        const b = positions.get(edge.to);
        if (!a || !b) return null;
        const key = `${edge.from}->${edge.to}`;
        const onPath = pathEdgeKeys.has(key);
        const d = edgeCurvePath(a, b, obstacles);
        const dim = edgeDim(edge.from, edge.to, focusId, focusNeighborIds, onPath);

        if (onPath) {
          return (
            <g key={key} className={`pathdle-edge-layer${dim}`}>
              <path
                d={d}
                fill="none"
                stroke="var(--accent)"
                strokeWidth={6}
                strokeLinecap="round"
                className="pathdle-edge-path-glow"
                opacity={0.42}
              />
              <path
                d={d}
                fill="none"
                stroke="var(--accent)"
                strokeWidth={3.6}
                strokeLinecap="round"
                className="pathdle-edge-in pathdle-edge-path"
              />
              <path
                d={d}
                fill="none"
                stroke="oklch(0.96 0.06 78)"
                strokeWidth={1.35}
                strokeLinecap="round"
                className="pathdle-edge-flow"
              />
            </g>
          );
        }

        return (
          <g key={key} className={`pathdle-edge-layer${dim}`}>
            <path
              d={d}
              fill="none"
              stroke="oklch(0.78 0.06 80 / 0.85)"
              strokeWidth={2.4}
              strokeLinecap="round"
              className="pathdle-edge-in pathdle-edge-branch"
            />
          </g>
        );
      })}

      {ghost?.kind === "fail" && (
        <g key={ghost.key} className="pathdle-fail-burst">
          <line
            x1={ghost.from.x}
            y1={ghost.from.y}
            x2={ghost.to.x}
            y2={ghost.to.y}
            className="stroke-[var(--fail)] stroke-[3.5] pathdle-edge-fail"
            strokeLinecap="round"
          />
          <circle
            cx={ghost.to.x}
            cy={ghost.to.y}
            r={24}
            className="pathdle-fail-ring fill-none stroke-[var(--fail)]"
          />
          <text
            x={(ghost.from.x + ghost.to.x) / 2}
            y={(ghost.from.y + ghost.to.y) / 2 - 16}
            textAnchor="middle"
            className="pathdle-fail-label fill-[var(--fail)]"
          >
            {ghost.label}
          </text>
        </g>
      )}

      {ghost?.kind === "pending" && (
        <line
          key={ghost.key}
          x1={ghost.from.x}
          y1={ghost.from.y}
          x2={ghost.to.x}
          y2={ghost.to.y}
          className="stroke-[var(--accent)] stroke-[3] opacity-70 pathdle-drag-line"
          strokeLinecap="round"
        />
      )}

      {dragLine && (
        <line
          x1={dragLine.from.x}
          y1={dragLine.from.y}
          x2={dragLine.to.x}
          y2={dragLine.to.y}
          stroke={dragLine.nearValid ? "var(--accent)" : "oklch(0.82 0.05 85 / 0.6)"}
          strokeWidth={dragLine.nearValid ? 3.4 : 2.5}
          strokeLinecap="round"
          strokeDasharray={dragLine.nearValid ? undefined : "6 6"}
          className="pathdle-drag-line"
          opacity={dragLine.nearValid ? 0.98 : 0.7}
        />
      )}
    </g>
  );
}
