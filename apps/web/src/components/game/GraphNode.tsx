"use client";

import type { CSSProperties, PointerEvent } from "react";
import type { PuzzleNode } from "@/lib/api/types";

type Props = {
  node: PuzzleNode;
  onPath: boolean;
  connected: boolean;
  selected: boolean;
  hovered: boolean;
  highlighted: boolean;
  revealed: boolean;
  approach: boolean;
  linkingFrom: boolean;
  justConnected: boolean;
  floatStyle: CSSProperties;
  disabled?: boolean;
  onPointerDown: (event: PointerEvent) => void;
  onPointerEnter: () => void;
  onPointerLeave: () => void;
};

function coreRadius(kind: PuzzleNode["kind"]) {
  if (kind === "start" || kind === "target") return 8;
  return 5.5;
}

export function GraphNode({
  node,
  onPath,
  connected,
  selected,
  hovered,
  highlighted,
  revealed,
  approach,
  linkingFrom,
  justConnected,
  floatStyle,
  disabled,
  onPointerDown,
  onPointerEnter,
  onPointerLeave,
}: Props) {
  const isStart = node.kind === "start";
  const isTarget = node.kind === "target";
  const r = coreRadius(node.kind);
  const hitR = r + 16;
  const active = selected || hovered || approach || linkingFrom;
  const accent = isStart
    ? "var(--accent)"
    : isTarget
      ? "var(--target)"
      : connected || onPath
        ? "var(--node-connected)"
        : "var(--node-core)";

  const glowOpacity = active ? 0.55 : connected || onPath ? 0.38 : 0.22;

  return (
    <g
      data-node-id={node.id}
      onPointerDown={onPointerDown}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
      style={{ cursor: disabled ? "default" : "crosshair" }}
    >
      <circle r={hitR} fill="transparent" />

      <g
        className={connected || onPath ? undefined : "pathdle-node-float"}
        style={connected || onPath ? undefined : floatStyle}
      >
        <g className={`pathdle-node-visual${active || approach ? " is-hover" : ""}`}>
          {highlighted && (
            <circle
              r={r + 14}
              className="pathdle-neighbor-pulse fill-none stroke-[var(--accent)]"
              strokeWidth={1.5}
              pointerEvents="none"
            />
          )}

          <circle
            r={r + (isStart || isTarget ? 14 : 10)}
            fill={accent}
            opacity={glowOpacity * 0.35}
            className={isTarget ? "pathdle-node-target-pulse" : "pathdle-node-halo"}
            pointerEvents="none"
          />

          {isStart && (
            <circle
              r={r + 11}
              fill="none"
              stroke="var(--accent)"
              strokeOpacity={0.4}
              strokeWidth={1}
              strokeDasharray="3 5"
              className="pathdle-node-ring"
              pointerEvents="none"
            />
          )}

          {isTarget && (
            <circle
              r={r + 11}
              fill="none"
              stroke="var(--target)"
              strokeOpacity={0.45}
              strokeWidth={1.25}
              className="pathdle-node-target-pulse"
              pointerEvents="none"
            />
          )}

          {(approach || linkingFrom) && (
            <circle
              r={r + 9}
              fill="none"
              stroke={accent}
              strokeOpacity={0.7}
              strokeWidth={1.25}
              pointerEvents="none"
            />
          )}

          {justConnected && (
            <circle
              r={r + 6}
              fill="none"
              stroke={accent}
              strokeWidth={2}
              className="pathdle-node-connect-flash"
              pointerEvents="none"
            />
          )}

          <circle
            r={r}
            fill={accent}
            opacity={isStart || isTarget ? 0.95 : connected || onPath ? 0.9 : 0.78}
            stroke={
              active || revealed
                ? "oklch(0.96 0.02 85 / 0.85)"
                : "oklch(0.2 0.03 250 / 0.5)"
            }
            strokeWidth={active || revealed ? 1.75 : 1}
            filter="url(#node-glow)"
            pointerEvents="none"
          />

          <circle
            cx={-r * 0.28}
            cy={-r * 0.32}
            r={r * 0.22}
            fill="oklch(0.98 0.01 85 / 0.55)"
            pointerEvents="none"
          />

          {(isStart || isTarget) && (
            <text
              y={-r - 16}
              textAnchor="middle"
              className="pathdle-node-tag"
              fill={isStart ? "var(--accent)" : "var(--target)"}
              pointerEvents="none"
            >
              {isStart ? "START" : "TARGET"}
            </text>
          )}

          <text
            y={r + 16}
            textAnchor="middle"
            className={`pathdle-node-label${active || connected || onPath || isStart || isTarget ? " is-active" : ""}`}
            pointerEvents="none"
          >
            {node.title}
          </text>
        </g>
      </g>
    </g>
  );
}

export function nodeHitRadius(kind: PuzzleNode["kind"]) {
  return coreRadius(kind) + 16;
}
