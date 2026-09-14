"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { CSSProperties, PointerEvent as ReactPointerEvent } from "react";
import type { Edge, PuzzleNode } from "@/lib/api/types";
import { SCORING } from "@/lib/api/types";
import { FieldAtmosphere } from "./FieldAtmosphere";
import { GraphEdges } from "./GraphEdges";
import { GraphNode, nodeHitRadius } from "./GraphNode";

const WORLD = 1000;
const PAD = 72;
const DRAG_THRESHOLD_PX = 10;

type Point = { x: number; y: number };

type DragState =
  | {
      mode: "pan";
      pointerId: number;
      startClientX: number;
      startClientY: number;
      originX: number;
      originY: number;
    }
  | {
      mode: "link";
      pointerId: number;
      fromId: string;
      startClientX: number;
      startClientY: number;
      moved: boolean;
      current: Point;
    };

type GhostLine = {
  from: Point;
  to: Point;
  kind: "fail" | "pending";
  key: number;
  label?: string;
};

type Props = {
  nodes: PuzzleNode[];
  discoveredEdges: Edge[];
  hintEdges: Edge[];
  playerPath: string[];
  revealedArticleIds: string[];
  highlightedNeighborIds: string[];
  menuNodeId: string | null;
  disabled?: boolean;
  onAttempt: (fromId: string, toId: string) => Promise<boolean>;
  onNodeClick: (nodeId: string) => void;
  onRevealRequest: (nodeId: string) => void;
  onCloseMenu: () => void;
  feedback?: string | null;
};

function toWorld(node: PuzzleNode): Point {
  return {
    x: PAD + node.x * (WORLD - PAD * 2),
    y: PAD + node.y * (WORLD - PAD * 2),
  };
}

function floatParams(id: string) {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) | 0;
  const n = Math.abs(h);
  return {
    "--float-dur": `${9 + (n % 5)}s`,
    "--float-delay": `${-((n % 80) / 10)}s`,
  } as CSSProperties;
}

function clientToSvgRoot(
  svg: SVGSVGElement,
  clientX: number,
  clientY: number,
): Point | null {
  const ctm = svg.getScreenCTM();
  if (!ctm) return null;
  const pt = svg.createSVGPoint();
  pt.x = clientX;
  pt.y = clientY;
  const local = pt.matrixTransform(ctm.inverse());
  return { x: local.x, y: local.y };
}

export function GameBoard({
  nodes,
  discoveredEdges,
  hintEdges,
  playerPath,
  revealedArticleIds,
  highlightedNeighborIds,
  menuNodeId,
  disabled = false,
  onAttempt,
  onNodeClick,
  onRevealRequest,
  onCloseMenu,
  feedback,
}: Props) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [view, setView] = useState({ x: 0, y: 0, k: 1 });
  const viewRef = useRef(view);
  const [drag, setDrag] = useState<DragState | null>(null);
  const dragRef = useRef<DragState | null>(null);
  const [ghost, setGhost] = useState<GhostLine | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [hoverId, setHoverId] = useState<string | null>(null);
  const [approachId, setApproachId] = useState<string | null>(null);
  const [justConnected, setJustConnected] = useState<Set<string>>(new Set());
  const ghostKey = useRef(0);

  useEffect(() => {
    viewRef.current = view;
  }, [view]);

  useEffect(() => {
    dragRef.current = drag;
  }, [drag]);

  const connectedIds = useMemo(() => {
    const next = new Set<string>();
    for (const e of discoveredEdges) {
      next.add(e.from);
      next.add(e.to);
    }
    return next;
  }, [discoveredEdges]);

  const positions = useMemo(() => {
    const map = new Map<string, Point>();
    for (const node of nodes) {
      map.set(node.id, toWorld(node));
    }
    return map;
  }, [nodes]);

  const discoveredKeys = useMemo(
    () => new Set(discoveredEdges.map((e) => `${e.from}->${e.to}`)),
    [discoveredEdges],
  );

  const visibleHints = useMemo(
    () => hintEdges.filter((e) => !discoveredKeys.has(`${e.from}->${e.to}`)),
    [hintEdges, discoveredKeys],
  );

  const pathSet = useMemo(() => new Set(playerPath), [playerPath]);
  const revealedSet = useMemo(
    () => new Set(revealedArticleIds),
    [revealedArticleIds],
  );
  const highlightSet = useMemo(
    () => new Set(highlightedNeighborIds),
    [highlightedNeighborIds],
  );

  const clientToWorld = useCallback((clientX: number, clientY: number): Point | null => {
    const svg = svgRef.current;
    if (!svg) return null;
    const root = clientToSvgRoot(svg, clientX, clientY);
    if (!root) return null;
    const { x, y, k } = viewRef.current;
    return {
      x: (root.x - x) / k,
      y: (root.y - y) / k,
    };
  }, []);

  const hitNode = useCallback(
    (world: Point, excludeId?: string): PuzzleNode | null => {
      let best: PuzzleNode | null = null;
      let bestDist = Infinity;
      for (const node of nodes) {
        if (excludeId && node.id === excludeId) continue;
        const p = positions.get(node.id);
        if (!p) continue;
        const r = nodeHitRadius(node.kind) + 6;
        const d = Math.hypot(world.x - p.x, world.y - p.y);
        if (d <= r && d < bestDist) {
          best = node;
          bestDist = d;
        }
      }
      return best;
    },
    [nodes, positions],
  );

  useEffect(() => {
    const svg = svgRef.current;
    if (!svg) return;

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      const root = clientToSvgRoot(svg, event.clientX, event.clientY);
      if (!root) return;
      const factor = event.deltaY < 0 ? 1.07 : 1 / 1.07;
      setView((prev) => {
        const nextK = Math.min(3.2, Math.max(0.55, prev.k * factor));
        const wx = (root.x - prev.x) / prev.k;
        const wy = (root.y - prev.y) / prev.k;
        return {
          k: nextK,
          x: root.x - wx * nextK,
          y: root.y - wy * nextK,
        };
      });
    };

    svg.addEventListener("wheel", onWheel, { passive: false });
    return () => svg.removeEventListener("wheel", onWheel);
  }, []);

  const playFail = (from: Point, to: Point) => {
    setGhost({
      from,
      to,
      kind: "fail",
      key: ++ghostKey.current,
      label: `+${SCORING.failedLink}`,
    });
    window.setTimeout(() => setGhost(null), 550);
  };

  const flashConnect = (fromId: string, toId: string) => {
    setJustConnected(new Set([fromId, toId]));
    window.setTimeout(() => setJustConnected(new Set()), 560);
  };

  const finishLink = async (fromId: string, toId: string, from: Point, to: Point) => {
    setDrag(null);
    setApproachId(null);
    if (fromId === toId) {
      setGhost(null);
      return;
    }

    setGhost({ from, to, kind: "pending", key: ++ghostKey.current });
    const ok = await onAttempt(fromId, toId);
    if (ok) {
      setGhost(null);
      setSelectedId(toId);
      flashConnect(fromId, toId);
    } else {
      playFail(from, to);
    }
  };

  const beginLink = (nodeId: string, event: ReactPointerEvent) => {
    if (disabled || event.button !== 0) return;
    event.stopPropagation();
    event.preventDefault();

    const world = clientToWorld(event.clientX, event.clientY) ?? positions.get(nodeId);
    if (!world) return;

    onCloseMenu();
    svgRef.current?.setPointerCapture(event.pointerId);
    setSelectedId(nodeId);
    setDrag({
      mode: "link",
      pointerId: event.pointerId,
      fromId: nodeId,
      startClientX: event.clientX,
      startClientY: event.clientY,
      moved: false,
      current: world,
    });
  };

  const beginPan = (event: ReactPointerEvent) => {
    if (disabled || event.button !== 0) return;
    onCloseMenu();
    event.preventDefault();
    svgRef.current?.setPointerCapture(event.pointerId);
    setSelectedId(null);
    setDrag({
      mode: "pan",
      pointerId: event.pointerId,
      startClientX: event.clientX,
      startClientY: event.clientY,
      originX: viewRef.current.x,
      originY: viewRef.current.y,
    });
  };

  const onBackgroundPointerDown = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (disabled || event.button !== 0) return;
    const target = event.target as Element;
    // Nodes and menus own the gesture; everything else pans the camera.
    if (target.closest("[data-node-id]")) return;
    if (target.closest("[data-node-menu]")) return;
    beginPan(event);
  };

  const onPointerMove = (event: ReactPointerEvent<SVGSVGElement>) => {
    const current = dragRef.current;
    if (!current || current.pointerId !== event.pointerId) return;

    if (current.mode === "pan") {
      const svg = svgRef.current;
      if (!svg) return;
      const start = clientToSvgRoot(svg, current.startClientX, current.startClientY);
      const now = clientToSvgRoot(svg, event.clientX, event.clientY);
      if (!start || !now) return;
      setView((prev) => ({
        ...prev,
        x: current.originX + (now.x - start.x),
        y: current.originY + (now.y - start.y),
      }));
      return;
    }

    const dist = Math.hypot(
      event.clientX - current.startClientX,
      event.clientY - current.startClientY,
    );
    const moved = current.moved || dist >= DRAG_THRESHOLD_PX;
    const world = clientToWorld(event.clientX, event.clientY);
    if (!world) return;
    setDrag({ ...current, moved, current: world });

    if (moved) {
      const near = hitNode(world, current.fromId);
      setApproachId(near?.id ?? null);
    }
  };

  const onPointerUp = async (event: ReactPointerEvent<SVGSVGElement>) => {
    const current = dragRef.current;
    if (!current || current.pointerId !== event.pointerId) return;

    if (current.mode === "pan") {
      setDrag(null);
      return;
    }

    if (!current.moved) {
      setDrag(null);
      setApproachId(null);
      onNodeClick(current.fromId);
      return;
    }

    const world = clientToWorld(event.clientX, event.clientY);
    const from = positions.get(current.fromId);
    if (!world || !from) {
      setDrag(null);
      setApproachId(null);
      return;
    }

    const target = hitNode(world, current.fromId);
    if (!target) {
      setDrag(null);
      setApproachId(null);
      setGhost(null);
      return;
    }

    await finishLink(
      current.fromId,
      target.id,
      from,
      positions.get(target.id)!,
    );
  };

  const menuNode = menuNodeId ? nodes.find((n) => n.id === menuNodeId) : null;
  const menuPos = menuNodeId ? positions.get(menuNodeId) : null;
  const linking = drag?.mode === "link" && drag.moved;
  const panning = drag?.mode === "pan";
  const dragFrom = linking && drag?.mode === "link" ? positions.get(drag.fromId) : null;

  return (
    <div className="relative h-full w-full overflow-hidden">
      <FieldAtmosphere />

      <svg
        ref={svgRef}
        viewBox={`0 0 ${WORLD} ${WORLD}`}
        preserveAspectRatio="xMidYMid meet"
        className={`relative z-[1] h-full w-full touch-none ${
          linking
            ? "cursor-crosshair"
            : panning
              ? "cursor-grabbing"
              : "cursor-grab active:cursor-grabbing"
        }`}
        role="application"
        aria-label="Pathdle board"
        onPointerDown={onBackgroundPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={() => {
          setDrag(null);
          setApproachId(null);
        }}
      >
        <defs>
          <filter id="node-glow" x="-80%" y="-80%" width="260%" height="260%">
            <feGaussianBlur stdDeviation="2.4" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        {/*
          Always-hittable pan surface (fill="transparent" is not reliably
          pointer-tested in some browsers). Covers the full viewBox so
          dragging empty space moves the camera.
        */}
        <rect
          data-pan-surface
          width={WORLD}
          height={WORLD}
          fill="black"
          fillOpacity={0}
          pointerEvents="all"
        />

        <g transform={`translate(${view.x} ${view.y}) scale(${view.k})`}>
          {/* Extra pan plane in world space so empty gaps stay draggable when zoomed */}
          <rect
            data-pan-surface
            x={-WORLD}
            y={-WORLD}
            width={WORLD * 3}
            height={WORLD * 3}
            fill="black"
            fillOpacity={0}
            pointerEvents="all"
          />

          <GraphEdges
            positions={positions}
            discoveredEdges={discoveredEdges}
            visibleHints={visibleHints}
            pathSet={pathSet}
            ghost={ghost}
            dragLine={
              linking && drag?.mode === "link" && dragFrom
                ? {
                    from: dragFrom,
                    to: drag.current,
                    nearValid: Boolean(approachId),
                  }
                : null
            }
          />

          {nodes.map((node) => {
            const p = positions.get(node.id)!;
            return (
              <g key={node.id} transform={`translate(${p.x} ${p.y})`}>
                <GraphNode
                  node={node}
                  onPath={pathSet.has(node.id)}
                  connected={connectedIds.has(node.id)}
                  selected={
                    selectedId === node.id || menuNodeId === node.id
                  }
                  hovered={hoverId === node.id}
                  highlighted={highlightSet.has(node.id)}
                  revealed={revealedSet.has(node.id)}
                  approach={approachId === node.id}
                  linkingFrom={linking && drag?.mode === "link" && drag.fromId === node.id}
                  justConnected={justConnected.has(node.id)}
                  floatStyle={floatParams(node.id)}
                  disabled={disabled}
                  onPointerDown={(event) => beginLink(node.id, event)}
                  onPointerEnter={() => setHoverId(node.id)}
                  onPointerLeave={() =>
                    setHoverId((id) => (id === node.id ? null : id))
                  }
                />
              </g>
            );
          })}

          {menuNode && menuPos && (
            <foreignObject
              data-node-menu
              x={menuPos.x + 22}
              y={menuPos.y - 48}
              width={210}
              height={108}
            >
              <div className="pathdle-hud-chip rounded-lg p-3 text-left shadow-xl">
                <p className="text-xs font-semibold text-[var(--ink-bright)]">
                  {menuNode.title}
                </p>
                <p className="mt-1 text-[0.68rem] leading-snug text-[var(--ink-muted)]">
                  Reveal outbound hints. Drag to confirm a real link.
                </p>
                <button
                  type="button"
                  className="mt-2 w-full rounded-md bg-[var(--hint)] px-2 py-1.5 text-xs font-semibold text-[oklch(0.14_0.03_250)] transition hover:brightness-110 disabled:opacity-50"
                  onClick={(event) => {
                    event.stopPropagation();
                    onRevealRequest(menuNode.id);
                  }}
                  disabled={revealedSet.has(menuNode.id)}
                >
                  {revealedSet.has(menuNode.id)
                    ? "Hints already shown"
                    : `Reveal hints (+${SCORING.revealOutbound})`}
                </button>
              </div>
            </foreignObject>
          )}
        </g>
      </svg>

      {feedback && (
        <p className="pathdle-feedback pointer-events-none absolute bottom-[5.5rem] left-1/2 z-10 -translate-x-1/2 pathdle-hud-chip rounded-full px-4 py-2 text-sm text-[var(--ink-bright)] sm:bottom-28">
          {feedback}
        </p>
      )}
    </div>
  );
}
