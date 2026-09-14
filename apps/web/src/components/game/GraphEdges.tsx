"use client";

import type { Edge } from "@/lib/api/types";

type Point = { x: number; y: number };

type GhostLine = {
  from: Point;
  to: Point;
  kind: "fail" | "pending";
  key: number;
  label?: string;
};

type Props = {
  positions: Map<string, Point>;
  discoveredEdges: Edge[];
  visibleHints: Edge[];
  pathSet: Set<string>;
  ghost: GhostLine | null;
  dragLine: { from: Point; to: Point; nearValid: boolean } | null;
};

export function GraphEdges({
  positions,
  discoveredEdges,
  visibleHints,
  pathSet,
  ghost,
  dragLine,
}: Props) {
  return (
    <g pointerEvents="none">
      {visibleHints.map((edge) => {
        const a = positions.get(edge.from);
        const b = positions.get(edge.to);
        if (!a || !b) return null;
        return (
          <g key={`hint-${edge.from}->${edge.to}`}>
            <line
              x1={a.x}
              y1={a.y}
              x2={b.x}
              y2={b.y}
              className="stroke-[var(--hint)] stroke-[1.75] pathdle-hint-edge"
              strokeDasharray="4 6"
              strokeLinecap="round"
              opacity={0.7}
            />
            <circle
              cx={(a.x + b.x) / 2}
              cy={(a.y + b.y) / 2}
              r={2.5}
              className="fill-[var(--hint)]"
              opacity={0.75}
            />
          </g>
        );
      })}

      {discoveredEdges.map((edge) => {
        const a = positions.get(edge.from);
        const b = positions.get(edge.to);
        if (!a || !b) return null;
        const onPath = pathSet.has(edge.from) && pathSet.has(edge.to);
        const stroke = onPath ? "var(--accent)" : "oklch(0.72 0.04 85 / 0.5)";
        return (
          <g key={`${edge.from}->${edge.to}`}>
            <line
              x1={a.x}
              y1={a.y}
              x2={b.x}
              y2={b.y}
              stroke={stroke}
              strokeWidth={onPath ? 2.75 : 2}
              strokeLinecap="round"
              className="pathdle-edge-in pathdle-edge-settled"
            />
            {/* Very subtle flowing dash on path edges only */}
            {onPath && (
              <line
                x1={a.x}
                y1={a.y}
                x2={b.x}
                y2={b.y}
                stroke="var(--accent)"
                strokeWidth={1.25}
                strokeLinecap="round"
                className="pathdle-edge-flow"
              />
            )}
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
            className="stroke-[var(--fail)] stroke-[3] pathdle-edge-fail"
            strokeLinecap="round"
          />
          <circle
            cx={ghost.to.x}
            cy={ghost.to.y}
            r={20}
            className="pathdle-fail-ring fill-none stroke-[var(--fail)]"
          />
          <text
            x={(ghost.from.x + ghost.to.x) / 2}
            y={(ghost.from.y + ghost.to.y) / 2 - 14}
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
          className="stroke-[var(--accent)] stroke-[2.5] opacity-60 pathdle-drag-line"
          strokeLinecap="round"
        />
      )}

      {dragLine && (
        <line
          x1={dragLine.from.x}
          y1={dragLine.from.y}
          x2={dragLine.to.x}
          y2={dragLine.to.y}
          stroke={dragLine.nearValid ? "var(--accent)" : "oklch(0.75 0.04 85 / 0.55)"}
          strokeWidth={dragLine.nearValid ? 2.75 : 2}
          strokeLinecap="round"
          strokeDasharray={dragLine.nearValid ? undefined : "5 5"}
          className="pathdle-drag-line"
          opacity={dragLine.nearValid ? 0.95 : 0.65}
        />
      )}
    </g>
  );
}
