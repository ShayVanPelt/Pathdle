"use client";

import { useMemo } from "react";
import type { Edge, PuzzleNode } from "@/lib/api/types";
import { undirectedEdgeKey } from "@/lib/game/charted";
import {
  groupSwatch,
  labelChipPath,
  nodeBody,
  placeGroupPlaques,
  PLAQUE_LINE_H,
  routeLink,
  type GroupPlaque,
  type LinkGeom,
  type Point,
  type Rect,
} from "@/lib/graph/layout";

type GhostLine = {
  from: Point;
  to: Point;
  kind: "fail" | "pending";
  key: number;
  label?: string;
};

type Props = {
  layer: "wires" | "plaques";
  nodes: PuzzleNode[];
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

function useLinkGeoms(
  edges: Edge[],
  nodes: PuzzleNode[],
  positions: Map<string, Point>,
  obstacles: Rect[],
) {
  const bodies = useMemo(
    () => new Map(nodes.map((n) => [n.id, nodeBody(n)])),
    [nodes],
  );

  return useMemo(() => {
    const geoms = new Map<string, LinkGeom>();
    for (const edge of edges) {
      const a = positions.get(edge.from);
      const b = positions.get(edge.to);
      const ba = bodies.get(edge.from);
      const bb = bodies.get(edge.to);
      if (!a || !b || !ba || !bb) continue;
      const key = undirectedEdgeKey(edge.from, edge.to);
      geoms.set(key, routeLink(edge.from, edge.to, a, b, ba, bb, obstacles));
    }
    return geoms;
  }, [edges, bodies, positions, obstacles]);
}

export function GraphEdges({
  layer,
  nodes,
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
  const hintGeoms = useLinkGeoms(visibleHints, nodes, positions, obstacles);
  const discoveredGeoms = useLinkGeoms(
    discoveredEdges,
    nodes,
    positions,
    obstacles,
  );

  const plaques = useMemo(() => {
    const items: {
      key: string;
      label: string;
      fromId: string;
      toId: string;
      geom: LinkGeom;
    }[] = [];
    for (const edge of discoveredEdges) {
      const key = undirectedEdgeKey(edge.from, edge.to);
      const geom = discoveredGeoms.get(key);
      const label = edge.groupLabel?.trim();
      if (!geom || !label) continue;
      items.push({ key, label, geom, fromId: edge.from, toId: edge.to });
    }
    return placeGroupPlaques(items, obstacles);
  }, [discoveredEdges, discoveredGeoms, obstacles]);

  const plaqueByKey = useMemo(() => {
    const map = new Map<string, GroupPlaque>();
    for (const plaque of plaques) map.set(plaque.key, plaque);
    return map;
  }, [plaques]);

  const discoveredByKey = useMemo(() => {
    const map = new Map<string, Edge>();
    for (const edge of discoveredEdges) {
      map.set(undirectedEdgeKey(edge.from, edge.to), edge);
    }
    return map;
  }, [discoveredEdges]);

  if (layer === "plaques") {
    return (
      <g pointerEvents="none">
        {discoveredEdges.map((edge) => {
          const key = undirectedEdgeKey(edge.from, edge.to);
          const geom = discoveredGeoms.get(key);
          if (!geom) return null;
          const onPath = pathEdgeKeys.has(key);
          const dim = edgeDim(
            edge.from,
            edge.to,
            focusId,
            focusNeighborIds,
            onPath,
          );
          const swatch = groupSwatch(edge.groupLabel?.trim() || key);
          const fill = swatch.dock;
          return (
            <g key={`dock-${key}`} className={`pathdle-edge-layer${dim}`}>
              <circle
                cx={geom.fromRim.x}
                cy={geom.fromRim.y}
                r={onPath ? 4.2 : 3.4}
                fill={fill}
                stroke="oklch(0.08 0.02 275 / 0.9)"
                strokeWidth={1.35}
              />
              <circle
                cx={geom.toRim.x}
                cy={geom.toRim.y}
                r={onPath ? 4.2 : 3.4}
                fill={fill}
                stroke="oklch(0.08 0.02 275 / 0.9)"
                strokeWidth={1.35}
              />
            </g>
          );
        })}

        {plaques.map((plaque) => {
          const onPath = pathEdgeKeys.has(plaque.key);
          const edge = discoveredByKey.get(plaque.key);
          const dim = edge
            ? edgeDim(edge.from, edge.to, focusId, focusNeighborIds, onPath)
            : "";
          const firstY = -plaque.h / 2 + 7 + PLAQUE_LINE_H * 0.72;
          return (
            <g
              key={`plaque-${plaque.key}`}
              className={`pathdle-edge-layer${dim}`}
              transform={`translate(${plaque.x} ${plaque.y})`}
            >
              {plaque.tick && (
                <line
                  x1={plaque.tick.x - plaque.x}
                  y1={plaque.tick.y - plaque.y}
                  x2={0}
                  y2={0}
                  stroke={plaque.swatch.stroke}
                  strokeWidth={1.2}
                  opacity={0.8}
                />
              )}
              <path
                d={labelChipPath(plaque.w, plaque.h, 8)}
                className="pathdle-group-plaque"
                fill={plaque.swatch.fill}
                stroke={plaque.swatch.stroke}
              />
              <text
                textAnchor="middle"
                className="pathdle-group-plaque-text"
                fill={plaque.swatch.text}
              >
                {plaque.lines.map((line, i) => (
                  <tspan key={`${line}-${i}`} x={0} y={firstY + i * PLAQUE_LINE_H}>
                    {line}
                  </tspan>
                ))}
              </text>
            </g>
          );
        })}
      </g>
    );
  }

  return (
    <g pointerEvents="none">
      {visibleHints.map((edge) => {
        const key = undirectedEdgeKey(edge.from, edge.to);
        const geom = hintGeoms.get(key);
        if (!geom) return null;
        return (
          <g
            key={`hint-${edge.from}->${edge.to}`}
            className={`pathdle-edge-layer${edgeDim(edge.from, edge.to, focusId, focusNeighborIds, false)}`}
          >
            <path
              d={geom.d}
              fill="none"
              className="pathdle-hint-edge stroke-[var(--hint)]"
              strokeWidth={2.35}
              strokeDasharray="7 9"
              strokeLinecap="round"
            />
          </g>
        );
      })}

      {discoveredEdges.map((edge) => {
        const key = undirectedEdgeKey(edge.from, edge.to);
        const geom = discoveredGeoms.get(key);
        if (!geom) return null;
        const onPath = pathEdgeKeys.has(key);
        const dim = edgeDim(
          edge.from,
          edge.to,
          focusId,
          focusNeighborIds,
          onPath,
        );
        const plaque = plaqueByKey.get(key);
        const swatch = groupSwatch(edge.groupLabel?.trim() || key);

        if (onPath) {
          return (
            <g key={key} className={`pathdle-edge-layer${dim}`}>
              <path
                d={geom.d}
                fill="none"
                stroke={swatch.halo}
                strokeWidth={9}
                strokeLinecap="round"
                opacity={0.9}
              />
              <path
                d={geom.d}
                fill="none"
                stroke="var(--accent)"
                strokeWidth={7}
                strokeLinecap="round"
                className="pathdle-edge-path-glow"
                opacity={0.32}
              />
              <path
                d={geom.d}
                fill="none"
                stroke="var(--accent)"
                strokeWidth={3.7}
                strokeLinecap="round"
                pathLength={1}
                className="pathdle-edge-in pathdle-edge-path"
              />
              <path
                d={geom.d}
                fill="none"
                stroke="oklch(0.96 0.06 78)"
                strokeWidth={1.35}
                strokeLinecap="round"
                className="pathdle-edge-flow"
              />
              {plaque && <title>{plaque.lines.join(" ")}</title>}
            </g>
          );
        }

        return (
          <g key={key} className={`pathdle-edge-layer${dim}`}>
            <path
              d={geom.d}
              fill="none"
              stroke={swatch.halo}
              strokeWidth={6.5}
              strokeLinecap="round"
            />
            <path
              d={geom.d}
              fill="none"
              stroke={swatch.stroke}
              strokeWidth={2.6}
              strokeLinecap="round"
              pathLength={1}
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
