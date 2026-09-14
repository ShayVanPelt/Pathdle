"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { CSSProperties, PointerEvent as ReactPointerEvent } from "react";
import type { Edge, PuzzleNode } from "@/lib/api/types";
import { SCORING } from "@/lib/api/types";
import {
  nodeBody,
  nodeRects,
  pathEdgeKeys,
  spreadDisplayPositions,
  type Point,
} from "@/lib/graph/layout";
import { CameraControls } from "./CameraControls";
import { ChartNote, type ChartNeighbor } from "./ChartNote";
import { FieldAtmosphere } from "./FieldAtmosphere";
import { GraphEdges } from "./GraphEdges";
import { GraphNode, resolveExploration } from "./GraphNode";

const WORLD = 1000;
const PAD = 88;
const DRAG_THRESHOLD_PX = 10;
const MIN_K = 0.5;
const MAX_K = 3.4;

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
  selectedId: string | null;
  menuNodeId: string | null;
  playLocked?: boolean;
  onSelectedChange: (id: string | null) => void;
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

function clampK(k: number) {
  return Math.min(MAX_K, Math.max(MIN_K, k));
}

export function GameBoard({
  nodes,
  discoveredEdges,
  hintEdges,
  playerPath,
  revealedArticleIds,
  highlightedNeighborIds,
  selectedId,
  menuNodeId,
  playLocked = false,
  onSelectedChange,
  onAttempt,
  onNodeClick,
  onRevealRequest,
  onCloseMenu,
  feedback,
}: Props) {
  const svgRef = useRef<SVGSVGElement>(null);
  const boardRef = useRef<HTMLDivElement>(null);
  const [view, setView] = useState({ x: 0, y: 0, k: 1 });
  const viewRef = useRef(view);
  const [drag, setDrag] = useState<DragState | null>(null);
  const dragRef = useRef<DragState | null>(null);
  const [ghost, setGhost] = useState<GhostLine | null>(null);
  const [hoverId, setHoverId] = useState<string | null>(null);
  const [approachId, setApproachId] = useState<string | null>(null);
  const [justConnected, setJustConnected] = useState<Set<string>>(new Set());
  const [notePos, setNotePos] = useState<CSSProperties | undefined>();
  const [compact, setCompact] = useState(false);
  const ghostKey = useRef(0);
  const pointersRef = useRef(new Map<number, Point>());
  const pinchRef = useRef<{
    dist: number;
    k: number;
    x: number;
    y: number;
    cx: number;
    cy: number;
  } | null>(null);

  useEffect(() => {
    const mq = window.matchMedia("(max-width: 639px)");
    const sync = () => setCompact(mq.matches);
    sync();
    mq.addEventListener("change", sync);
    return () => mq.removeEventListener("change", sync);
  }, []);

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

  const apiPositions = useMemo(() => {
    const map = new Map<string, Point>();
    for (const node of nodes) {
      map.set(node.id, toWorld(node));
    }
    return map;
  }, [nodes]);

  const startId = useMemo(
    () => nodes.find((n) => n.kind === "start")?.id,
    [nodes],
  );
  const targetId = useMemo(
    () => nodes.find((n) => n.kind === "target")?.id,
    [nodes],
  );

  const positions = useMemo(() => {
    return spreadDisplayPositions(nodes, apiPositions, startId, WORLD);
  }, [nodes, apiPositions, startId]);

  const obstacles = useMemo(
    () => nodeRects(nodes, positions),
    [nodes, positions],
  );

  const discoveredKeys = useMemo(
    () => new Set(discoveredEdges.map((e) => `${e.from}->${e.to}`)),
    [discoveredEdges],
  );

  const visibleHints = useMemo(
    () => hintEdges.filter((e) => !discoveredKeys.has(`${e.from}->${e.to}`)),
    [hintEdges, discoveredKeys],
  );

  const pathSet = useMemo(() => new Set(playerPath), [playerPath]);
  const traversedEdgeKeys = useMemo(
    () => pathEdgeKeys(playerPath),
    [playerPath],
  );
  const revealedSet = useMemo(
    () => new Set(revealedArticleIds),
    [revealedArticleIds],
  );
  const highlightSet = useMemo(
    () => new Set(highlightedNeighborIds),
    [highlightedNeighborIds],
  );

  const focusNeighborIds = useMemo(() => {
    const next = new Set<string>();
    if (!selectedId) return next;
    next.add(selectedId);
    for (const e of discoveredEdges) {
      if (e.from === selectedId) next.add(e.to);
      if (e.to === selectedId) next.add(e.from);
    }
    for (const e of hintEdges) {
      if (e.from === selectedId) next.add(e.to);
      if (e.to === selectedId) next.add(e.from);
    }
    return next;
  }, [selectedId, discoveredEdges, hintEdges]);

  const zoomAround = useCallback((nextK: number, root: Point) => {
    setView((prev) => {
      const k = clampK(nextK);
      const wx = (root.x - prev.x) / prev.k;
      const wy = (root.y - prev.y) / prev.k;
      return { k, x: root.x - wx * k, y: root.y - wy * k };
    });
  }, []);

  const zoomBy = useCallback(
    (factor: number) => {
      const svg = svgRef.current;
      const root = svg
        ? clientToSvgRoot(
            svg,
            svg.getBoundingClientRect().left + svg.clientWidth / 2,
            svg.getBoundingClientRect().top + svg.clientHeight / 2,
          )
        : { x: WORLD / 2, y: WORLD / 2 };
      if (!root) return;
      zoomAround(viewRef.current.k * factor, root);
    },
    [zoomAround],
  );

  const centerOnPoint = useCallback((p: Point, k = viewRef.current.k) => {
    setView({
      k: clampK(k),
      x: WORLD / 2 - p.x * clampK(k),
      y: WORLD / 2 - p.y * clampK(k),
    });
  }, []);

  const recenter = useCallback(() => {
    const ids = new Set(playerPath);
    if (startId) ids.add(startId);
    if (targetId) ids.add(targetId);
    const pts: Point[] = [];
    for (const id of ids) {
      const p = positions.get(id);
      if (p) pts.push(p);
    }
    if (pts.length === 0) {
      setView({ x: 0, y: 0, k: 1 });
      return;
    }
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const p of pts) {
      minX = Math.min(minX, p.x);
      minY = Math.min(minY, p.y);
      maxX = Math.max(maxX, p.x);
      maxY = Math.max(maxY, p.y);
    }
    const pad = 120;
    const bw = Math.max(80, maxX - minX + pad);
    const bh = Math.max(80, maxY - minY + pad);
    const k = clampK(Math.min(WORLD / bw, WORLD / bh, 1.55));
    centerOnPoint({ x: (minX + maxX) / 2, y: (minY + maxY) / 2 }, k);
  }, [playerPath, startId, targetId, positions, centerOnPoint]);

  const didFit = useRef(false);
  useEffect(() => {
    if (didFit.current || positions.size === 0) return;
    didFit.current = true;
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const node of nodes) {
      const p = positions.get(node.id);
      if (!p) continue;
      const b = nodeBody(node);
      minX = Math.min(minX, p.x - b.w / 2);
      minY = Math.min(minY, p.y - b.h / 2);
      maxX = Math.max(maxX, p.x + b.w / 2);
      maxY = Math.max(maxY, p.y + b.h / 2);
    }
    if (!Number.isFinite(minX)) return;
    minX -= 48;
    minY -= 108;
    maxX += 48;
    maxY += 72;
    const bw = Math.max(80, maxX - minX);
    const bh = Math.max(80, maxY - minY);
    const k = clampK(Math.min(WORLD / bw, WORLD / bh, 1.15));
    centerOnPoint({ x: (minX + maxX) / 2, y: (minY + maxY) / 2 }, k);
  }, [positions, nodes, centerOnPoint]);

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
        const b = nodeBody(node);
        const dx = world.x - p.x;
        const dy = world.y - p.y;
        if (Math.abs(dx) > b.w / 2 + 8 || Math.abs(dy) > b.h / 2 + 8) continue;
        const d = Math.hypot(dx, dy);
        if (d < bestDist) {
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
      const factor = event.deltaY < 0 ? 1.08 : 1 / 1.08;
      zoomAround(viewRef.current.k * factor, root);
    };

    svg.addEventListener("wheel", onWheel, { passive: false });
    return () => svg.removeEventListener("wheel", onWheel);
  }, [zoomAround]);

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
      onSelectedChange(toId);
      flashConnect(fromId, toId);
    } else {
      playFail(from, to);
    }
  };

  const beginLink = (nodeId: string, event: ReactPointerEvent) => {
    if (event.button !== 0) return;
    event.stopPropagation();
    event.preventDefault();

    const world = clientToWorld(event.clientX, event.clientY) ?? positions.get(nodeId);
    if (!world) return;

    onCloseMenu();
    onSelectedChange(nodeId);
    if (playLocked) {
      onNodeClick(nodeId);
      return;
    }

    svgRef.current?.setPointerCapture(event.pointerId);
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
    if (event.button !== 0) return;
    onCloseMenu();
    event.preventDefault();
    svgRef.current?.setPointerCapture(event.pointerId);
    onSelectedChange(null);
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
    if (event.button !== 0) return;
    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (pointersRef.current.size >= 2) {
      const pts = [...pointersRef.current.values()];
      const dist = Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y);
      const svg = svgRef.current;
      const mid = svg
        ? clientToSvgRoot(svg, (pts[0].x + pts[1].x) / 2, (pts[0].y + pts[1].y) / 2)
        : null;
      if (mid) {
        pinchRef.current = {
          dist,
          k: viewRef.current.k,
          x: viewRef.current.x,
          y: viewRef.current.y,
          cx: mid.x,
          cy: mid.y,
        };
      }
      setDrag(null);
      return;
    }
    const target = event.target as Element;
    if (target.closest("[data-node-id]")) return;
    if (target.closest("[data-node-menu]")) return;
    beginPan(event);
  };

  const onPointerMove = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (pointersRef.current.has(event.pointerId)) {
      pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
    }

    if (pinchRef.current && pointersRef.current.size >= 2) {
      const pts = [...pointersRef.current.values()];
      const dist = Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y);
      if (dist > 4 && pinchRef.current.dist > 4) {
        zoomAround(
          pinchRef.current.k * (dist / pinchRef.current.dist),
          { x: pinchRef.current.cx, y: pinchRef.current.cy },
        );
      }
      return;
    }

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

    if (playLocked) return;

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
    pointersRef.current.delete(event.pointerId);
    if (pointersRef.current.size < 2) pinchRef.current = null;

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

  useEffect(() => {
    if (!menuNodeId || !menuPos || !svgRef.current || !boardRef.current) {
      setNotePos(undefined);
      return;
    }
    const svg = svgRef.current;
    const board = boardRef.current;
    const ctm = svg.getScreenCTM();
    if (!ctm) return;
    const pt = svg.createSVGPoint();
    pt.x = view.x + menuPos.x * view.k;
    pt.y = view.y + menuPos.y * view.k;
    const screen = pt.matrixTransform(ctm);
    const box = board.getBoundingClientRect();
    const left = Math.min(Math.max(16, screen.x - box.left + 56), box.width - 380);
    const top = Math.min(Math.max(88, screen.y - box.top - 110), box.height - 340);
    setNotePos({ left, top });
  }, [menuNodeId, menuPos, view]);

  const neighbors: ChartNeighbor[] = useMemo(() => {
    if (!menuNodeId) return [];
    const list: ChartNeighbor[] = [];
    const seen = new Set<string>();
    for (const e of discoveredEdges) {
      if (e.from !== menuNodeId || seen.has(e.to)) continue;
      seen.add(e.to);
      const node = nodes.find((n) => n.id === e.to);
      list.push({ id: e.to, title: node?.title ?? e.to, kind: "path" });
    }
    for (const e of visibleHints) {
      if (e.from !== menuNodeId || seen.has(e.to)) continue;
      seen.add(e.to);
      const node = nodes.find((n) => n.id === e.to);
      list.push({ id: e.to, title: node?.title ?? e.to, kind: "hint" });
    }
    return list;
  }, [menuNodeId, discoveredEdges, visibleHints, nodes]);

  return (
    <div ref={boardRef} className="relative h-full w-full overflow-hidden">
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
        onPointerCancel={(event) => {
          pointersRef.current.delete(event.pointerId);
          pinchRef.current = null;
          setDrag(null);
          setApproachId(null);
        }}
      >
        <defs>
          <filter id="node-glow" x="-80%" y="-80%" width="260%" height="260%">
            <feGaussianBlur stdDeviation="2.8" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <rect
          data-pan-surface
          width={WORLD}
          height={WORLD}
          fill="black"
          fillOpacity={0}
          pointerEvents="all"
        />

        <g transform={`translate(${view.x} ${view.y}) scale(${view.k})`}>
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
            obstacles={obstacles}
            discoveredEdges={discoveredEdges}
            visibleHints={visibleHints}
            pathEdgeKeys={traversedEdgeKeys}
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
            focusId={selectedId}
            focusNeighborIds={focusNeighborIds}
          />

          {nodes.map((node) => {
            const p = positions.get(node.id);
            if (!p) return null;
            const onPath = pathSet.has(node.id);
            const connected = connectedIds.has(node.id);
            const exploration = resolveExploration(node, onPath, connected);
            const selected = selectedId === node.id || menuNodeId === node.id;
            const dimmed = Boolean(
              selectedId &&
                !focusNeighborIds.has(node.id) &&
                node.kind !== "start" &&
                node.kind !== "target" &&
                !onPath,
            );
            return (
              <g key={node.id} transform={`translate(${p.x} ${p.y})`}>
                <GraphNode
                  node={node}
                  exploration={exploration}
                  selected={selected}
                  hovered={hoverId === node.id}
                  highlighted={highlightSet.has(node.id)}
                  revealed={revealedSet.has(node.id)}
                  approach={approachId === node.id}
                  linkingFrom={linking && drag?.mode === "link" && drag.fromId === node.id}
                  justConnected={justConnected.has(node.id)}
                  dimmed={dimmed}
                  playLocked={playLocked}
                  onPointerDown={(event) => beginLink(node.id, event)}
                  onPointerEnter={() => setHoverId(node.id)}
                  onPointerLeave={() =>
                    setHoverId((id) => (id === node.id ? null : id))
                  }
                />
              </g>
            );
          })}
        </g>
      </svg>

      {menuNode && (compact || notePos) && (
        <ChartNote
          title={menuNode.title}
          neighbors={neighbors}
          revealed={revealedSet.has(menuNode.id)}
          playLocked={playLocked}
          variant={compact ? "sheet" : "popover"}
          style={compact ? undefined : notePos}
          onReveal={() => onRevealRequest(menuNode.id)}
          onClose={onCloseMenu}
        />
      )}

      <div className="pointer-events-none absolute bottom-5 right-3 z-20 sm:bottom-6 sm:right-5">
        <CameraControls
          onZoomIn={() => zoomBy(1.18)}
          onZoomOut={() => zoomBy(1 / 1.18)}
          onRecenter={recenter}
        />
      </div>

      {feedback && (
        <p className="pathdle-feedback pointer-events-none absolute bottom-28 left-1/2 z-10 -translate-x-1/2 pathdle-hud-chip rounded-full px-5 py-2.5 text-base text-[var(--ink-bright)] sm:bottom-10">
          {feedback}
        </p>
      )}
    </div>
  );
}
