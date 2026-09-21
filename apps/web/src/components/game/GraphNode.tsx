"use client";

import type { PointerEvent } from "react";
import type { PuzzleNode } from "@/lib/api/types";
import {
  labelChipPath,
  LINE_H,
  nodeBody,
  PAD_Y,
  TAG_H,
} from "@/lib/graph/layout";

export type NodeExploration = "start" | "target" | "path" | "branch" | "option";

type Props = {
  node: PuzzleNode;
  exploration: NodeExploration;
  canInitiateLink: boolean;
  selected: boolean;
  hovered: boolean;
  highlighted: boolean;
  revealed: boolean;
  approach: boolean;
  linkingFrom: boolean;
  justConnected: boolean;
  dimmed: boolean;
  playLocked?: boolean;
  onPointerDown: (event: PointerEvent) => void;
  onPointerEnter: () => void;
  onPointerLeave: () => void;
};

export function GraphNode({
  node,
  exploration,
  canInitiateLink,
  selected,
  hovered,
  highlighted,
  revealed,
  approach,
  linkingFrom,
  justConnected,
  dimmed,
  playLocked,
  onPointerDown,
  onPointerEnter,
  onPointerLeave,
}: Props) {
  const isStart = exploration === "start";
  const isTarget = exploration === "target";
  const onPath = exploration === "path" || isStart || isTarget;
  const isBranch = exploration === "branch";
  const isOption = exploration === "option";
  const body = nodeBody(node);
  const active = selected || hovered || approach || linkingFrom;

  const accent = isStart
    ? "var(--accent)"
    : isTarget
      ? "var(--target)"
      : onPath
        ? "var(--node-path)"
        : isBranch
          ? "var(--node-connected)"
          : "var(--node-core)";

  const firstLineY =
    -body.h / 2 + PAD_Y + (body.hasTag ? TAG_H : 0) + LINE_H * 0.72;

  return (
    <g
      data-node-id={node.id}
      role="button"
      aria-label={
        isStart ? `START ${node.title}` : isTarget ? `TARGET ${node.title}` : node.title
      }
      className={`pathdle-node-layer${dimmed ? " is-dimmed" : ""}`}
      onPointerDown={onPointerDown}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
      style={{
        cursor: playLocked ? "default" : canInitiateLink ? "crosshair" : "pointer",
      }}
    >
      <rect
        x={-body.w / 2 - 8}
        y={-body.h / 2 - 8}
        width={body.w + 16}
        height={body.h + 16}
        fill="transparent"
      />

      <g
        className={`pathdle-node-visual${active ? " is-hover" : ""}${isStart ? " is-start" : isTarget ? " is-target" : onPath ? " is-on-path" : ""}${isOption ? " is-option" : ""}`}
      >
        {highlighted && (
          <rect
            x={-body.w / 2 - 10}
            y={-body.h / 2 - 10}
            width={body.w + 20}
            height={body.h + 20}
            rx={body.r + 8}
            className="pathdle-neighbor-pulse fill-none stroke-[var(--hint)]"
            strokeWidth={2}
            pointerEvents="none"
          />
        )}

        {(isStart || isTarget) && (
          <>
            <path
              d={labelChipPath(body.w + 28, body.h + 26, body.r + 12)}
              fill="none"
              className={`pathdle-pole-orbit${isStart ? " is-start" : " is-target"}`}
              pointerEvents="none"
            />
            <path
              d={labelChipPath(body.w + 14, body.h + 14, body.r + 7)}
              fill="none"
              className={`pathdle-pole-ring${isStart ? " is-start" : " is-target"}`}
              pointerEvents="none"
            />
          </>
        )}

        <ellipse
          rx={body.w * (isStart || isTarget ? 0.58 : 0.42)}
          ry={body.h * (isStart || isTarget ? 0.72 : 0.55)}
          fill={accent}
          opacity={active ? 0.34 : isStart || isTarget ? 0.32 : onPath ? 0.2 : 0.1}
          className={
            isTarget
              ? "pathdle-node-target-pulse"
              : isStart
                ? "pathdle-node-start-pulse"
                : "pathdle-node-halo"
          }
          pointerEvents="none"
        />

        {justConnected && (
          <rect
            x={-body.w / 2 - 6}
            y={-body.h / 2 - 6}
            width={body.w + 12}
            height={body.h + 12}
            rx={body.r + 4}
            fill="none"
            stroke={accent}
            strokeWidth={2.5}
            className="pathdle-node-connect-flash"
            pointerEvents="none"
          />
        )}

        <path
          d={labelChipPath(body.w, body.h, body.r)}
          className={`pathdle-node-body${isStart ? " is-start" : isTarget ? " is-target" : onPath ? " is-path" : isBranch ? " is-branch" : ""}${selected ? " is-selected" : ""}`}
          stroke={
            selected
              ? "var(--focus)"
              : isStart
                ? "var(--accent)"
                : isTarget
                  ? "var(--target)"
                  : active || revealed
                    ? accent
                    : onPath
                      ? "var(--accent)"
                      : isBranch
                        ? "oklch(0.82 0.06 80 / 0.75)"
                        : "oklch(0.7 0.04 250 / 0.45)"
          }
          strokeWidth={isStart || isTarget ? 3.1 : onPath || selected ? 2.6 : active ? 2.1 : 1.6}
          filter={
            isStart
              ? "url(#start-lamp)"
              : isTarget
                ? "url(#target-lamp)"
                : isOption
                  ? undefined
                  : "url(#node-glow)"
          }
        />

        {body.hasTag && (
          <text
            y={-body.h / 2 + 22}
            textAnchor="middle"
            className={`pathdle-node-tag${isStart ? " is-start" : " is-target"}`}
            fill={isStart ? "var(--accent)" : "var(--target)"}
          >
            {isStart ? "START" : "TARGET"}
          </text>
        )}

        <text
          textAnchor="middle"
          className={`pathdle-node-label${onPath || isStart || isTarget || active ? " is-active" : isBranch ? " is-branch" : ""}`}
        >
          {body.lines.map((line, i) => (
            <tspan key={`${line}-${i}`} x={0} y={firstLineY + i * LINE_H}>
              {line}
            </tspan>
          ))}
        </text>
      </g>
    </g>
  );
}

export function resolveExploration(
  node: PuzzleNode,
  onPath: boolean,
  connected: boolean,
): NodeExploration {
  if (node.kind === "start") return "start";
  if (node.kind === "target") return "target";
  if (onPath) return "path";
  if (connected) return "branch";
  return "option";
}
