import type { NodeKind, PuzzleNode } from "@/lib/api/types";

export type Point = { x: number; y: number };

export type Rect = {
  x: number;
  y: number;
  w: number;
  h: number;
  cx: number;
  cy: number;
};

export type NodeBody = {
  w: number;
  h: number;
  r: number;
  lines: string[];
  hasTag: boolean;
};

const CHAR_W = 8.6;
const LINE_H = 18;
const PAD_X = 18;
const PAD_Y = 14;
const TAG_H = 16;
const MAX_LINE_CHARS = 15;
const MAX_LINES = 3;
const GAP = 30;

export function wrapTitle(
  title: string,
  maxChars = MAX_LINE_CHARS,
  maxLines = MAX_LINES,
): string[] {
  const words = title.split(/\s+/).filter(Boolean);
  const lines: string[] = [];
  let cur = "";
  for (const word of words) {
    const next = cur ? `${cur} ${word}` : word;
    if (next.length > maxChars && cur) {
      lines.push(cur);
      cur = word;
    } else {
      cur = next;
    }
  }
  if (cur) lines.push(cur);
  if (lines.length === 0) return [title];
  if (lines.length <= maxLines) return lines;
  const kept = lines.slice(0, maxLines);
  const last = kept[maxLines - 1];
  kept[maxLines - 1] =
    last.length >= maxChars ? `${last.slice(0, maxChars - 1)}…` : `${last}…`;
  return kept;
}

export function nodeBody(node: PuzzleNode): NodeBody {
  const hasTag = node.kind === "start" || node.kind === "target";
  const lines = wrapTitle(node.title, hasTag ? 14 : 15);
  const maxChars = Math.max(...lines.map((l) => l.length), hasTag ? 7 : 5);
  const w = Math.min(248, Math.max(108, maxChars * CHAR_W + PAD_X * 2));
  const h = PAD_Y * 2 + lines.length * LINE_H + (hasTag ? TAG_H : 0);
  return { w, h, r: Math.min(24, h / 2), lines, hasTag };
}

export function nodeHitRadius(node: PuzzleNode) {
  const b = nodeBody(node);
  return Math.max(b.w, b.h) / 2 + 10;
}

export function nodeDisplayRadius(kind: NodeKind) {
  if (kind === "start" || kind === "target") return 52;
  return 40;
}

function rectFromCenter(cx: number, cy: number, w: number, h: number): Rect {
  return { cx, cy, w, h, x: cx - w / 2, y: cy - h / 2 };
}

function lineIntersectsRect(
  ax: number,
  ay: number,
  bx: number,
  by: number,
  rect: Rect,
  pad = 8,
): boolean {
  const left = rect.x - pad;
  const right = rect.x + rect.w + pad;
  const top = rect.y - pad;
  const bottom = rect.y + rect.h + pad;

  function inside(x: number, y: number) {
    return x >= left && x <= right && y >= top && y <= bottom;
  }
  if (inside(ax, ay) || inside(bx, by)) return true;

  const dx = bx - ax;
  const dy = by - ay;
  const checks = [
    { x1: left, y1: top, x2: right, y2: top },
    { x1: right, y1: top, x2: right, y2: bottom },
    { x1: right, y1: bottom, x2: left, y2: bottom },
    { x1: left, y1: bottom, x2: left, y2: top },
  ];
  for (const seg of checks) {
    const denom = (seg.y2 - seg.y1) * dx - (seg.x2 - seg.x1) * dy;
    if (Math.abs(denom) < 1e-6) continue;
    const ua =
      ((seg.x2 - seg.x1) * (ay - seg.y1) - (seg.y2 - seg.y1) * (ax - seg.x1)) /
      denom;
    const ub =
      ((bx - ax) * (ay - seg.y1) - (by - ay) * (ax - seg.x1)) / denom;
    if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1) return true;
  }
  return false;
}

function hashAngle(id: string) {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 33 + id.charCodeAt(i)) | 0;
  return ((Math.abs(h) % 1000) / 1000) * Math.PI * 2;
}

function wrapAngle(a: number) {
  const tau = Math.PI * 2;
  return ((a % tau) + tau) % tau;
}

type Polar = { id: string; angle: number; radius: number };

/**
 * Display-only: keep each node's API direction, flatten onto a wide ring,
 * then space around the circle so pills do not clump.
 */
export function spreadDisplayPositions(
  nodes: PuzzleNode[],
  apiPositions: Map<string, Point>,
  _startId?: string,
  world = 1000,
): Map<string, Point> {
  const bodies = new Map(nodes.map((n) => [n.id, nodeBody(n)]));
  const cx = world / 2;
  const cy = world / 2 + 12;

  let gx = 0;
  let gy = 0;
  for (const node of nodes) {
    const p = apiPositions.get(node.id)!;
    gx += p.x;
    gy += p.y;
  }
  gx /= Math.max(1, nodes.length);
  gy /= Math.max(1, nodes.length);

  let maxDist = 1;
  for (const node of nodes) {
    const p = apiPositions.get(node.id)!;
    maxDist = Math.max(maxDist, Math.hypot(p.x - gx, p.y - gy));
  }

  const ring = Math.min(world * 0.34, 340);
  const ringRadius = [ring * 0.48, ring * 0.92, ring * 1.26];

  const polars: Polar[] = nodes.map((node) => {
    const p = apiPositions.get(node.id)!;
    const dx = p.x - gx;
    const dy = p.y - gy;
    const dist = Math.hypot(dx, dy);
    const angle = dist < 2 ? hashAngle(node.id) : Math.atan2(dy, dx);
    const t = dist / maxDist;
    let band = t < 0.38 ? 0 : t < 0.7 ? 1 : 2;
    if (node.kind === "start") band = 0;
    if (node.kind === "target") band = 2;
    let radius = ringRadius[band];
    if (node.kind === "start") radius = ring * 0.32;
    return {
      id: node.id,
      angle: wrapAngle(angle),
      radius,
    };
  });

  const bands = [0, 1, 2].map((band) =>
    polars.filter((p) => Math.abs(p.radius - ringRadius[band]) < 1),
  );

  for (const group of bands) {
    if (group.length === 0) continue;
    group.sort((a, b) => a.angle - b.angle);
    const count = group.length;
    const span = (Math.PI * 2) / count;
    const offset = group.reduce((s, p) => s + p.angle, 0) / count - ((count - 1) * span) / 2;
    for (let i = 0; i < count; i++) {
      const even = wrapAngle(offset + i * span);
      const orig = group[i].angle;
      let delta = even - orig;
      if (delta > Math.PI) delta -= Math.PI * 2;
      if (delta < -Math.PI) delta += Math.PI * 2;
      group[i].angle = wrapAngle(orig + delta * 0.72);
    }

    for (let iter = 0; iter < 50; iter++) {
      group.sort((a, b) => a.angle - b.angle);
      for (let i = 0; i < group.length; i++) {
        const a = group[i];
        const b = group[(i + 1) % group.length];
        const wa = bodies.get(a.id)!.w;
        const wb = bodies.get(b.id)!.w;
        const meanR = (a.radius + b.radius) / 2;
        const minGap = (wa / 2 + wb / 2 + GAP) / Math.max(140, meanR);
        let gap = b.angle - a.angle;
        if (i === group.length - 1) gap += Math.PI * 2;
        if (gap >= minGap) continue;
        const extra = (minGap - gap) / 2;
        a.angle = wrapAngle(a.angle - extra);
        b.angle = wrapAngle(b.angle + extra);
      }
    }
  }

  const current = new Map<string, Point>();
  for (const polar of polars) {
    current.set(polar.id, {
      x: cx + Math.cos(polar.angle) * polar.radius,
      y: cy + Math.sin(polar.angle) * polar.radius,
    });
  }

  const ids = nodes.map((n) => n.id);
  for (let iter = 0; iter < 80; iter++) {
    for (let i = 0; i < ids.length; i++) {
      for (let j = i + 1; j < ids.length; j++) {
        const idA = ids[i];
        const idB = ids[j];
        const a = bodies.get(idA)!;
        const b = bodies.get(idB)!;
        const pa = current.get(idA)!;
        const pb = current.get(idB)!;
        const dx = pb.x - pa.x;
        const dy = pb.y - pa.y;
        const ox = (a.w + b.w) / 2 + GAP - Math.abs(dx);
        const oy = (a.h + b.h) / 2 + GAP - Math.abs(dy);
        if (ox <= 0 || oy <= 0) continue;

        const dirX = Math.sign(dx || 1);
        const dirY = Math.sign(dy || 1);
        current.set(idA, {
          x: pa.x - dirX * ox * 0.32,
          y: pa.y - dirY * oy * 0.32,
        });
        current.set(idB, {
          x: pb.x + dirX * ox * 0.32,
          y: pb.y + dirY * oy * 0.32,
        });
      }
    }

    for (const id of ids) {
      const body = bodies.get(id)!;
      const p = current.get(id)!;
      const vx = p.x - cx;
      const vy = p.y - cy;
      const dist = Math.hypot(vx, vy) || 1;
      const minR = ring * 0.42;
      const maxR = ring * 1.28;
      const clamped = Math.min(maxR, Math.max(minR, dist));
      const nx = cx + (vx / dist) * clamped;
      const ny = cy + (vy / dist) * clamped;
      current.set(id, {
        x: Math.min(world - body.w / 2 - 16, Math.max(body.w / 2 + 16, nx)),
        y: Math.min(world - body.h / 2 - 16, Math.max(body.h / 2 + 16, ny)),
      });
    }
  }

  return current;
}

export function nodeRects(
  nodes: PuzzleNode[],
  positions: Map<string, Point>,
): Rect[] {
  return nodes.map((node) => {
    const p = positions.get(node.id)!;
    const b = nodeBody(node);
    return rectFromCenter(p.x, p.y, b.w, b.h);
  });
}

/** Quadratic curve that bulges away from node bodies. */
export function edgeCurvePath(
  from: Point,
  to: Point,
  obstacles: Rect[],
): string {
  const mx = (from.x + to.x) / 2;
  const my = (from.y + to.y) / 2;
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const len = Math.hypot(dx, dy) || 1;
  const nx = -dy / len;
  const ny = dx / len;

  let bulge = 0;
  for (const rect of obstacles) {
    if (!lineIntersectsRect(from.x, from.y, to.x, to.y, rect)) continue;
    const toRectX = rect.cx - mx;
    const toRectY = rect.cy - my;
    const side = toRectX * nx + toRectY * ny;
    bulge += side >= 0 ? 42 : -42;
  }

  if (Math.abs(bulge) < 1) {
    return `M ${from.x} ${from.y} L ${to.x} ${to.y}`;
  }

  return `M ${from.x} ${from.y} Q ${mx + nx * bulge} ${my + ny * bulge} ${to.x} ${to.y}`;
}

export function labelChipPath(w: number, h: number, r = 7): string {
  const hw = w / 2;
  const hh = h / 2;
  const cr = Math.min(r, hw, hh);
  return [
    `M ${-hw + cr} ${-hh}`,
    `H ${hw - cr}`,
    `Q ${hw} ${-hh} ${hw} ${-hh + cr}`,
    `V ${hh - cr}`,
    `Q ${hw} ${hh} ${hw - cr} ${hh}`,
    `H ${-hw + cr}`,
    `Q ${-hw} ${hh} ${-hw} ${hh - cr}`,
    `V ${-hh + cr}`,
    `Q ${-hw} ${-hh} ${-hw + cr} ${-hh}`,
    "Z",
  ].join(" ");
}

export function pathEdgeKeys(playerPath: string[]): Set<string> {
  const keys = new Set<string>();
  for (let i = 0; i < playerPath.length - 1; i++) {
    keys.add(`${playerPath[i]}->${playerPath[i + 1]}`);
  }
  return keys;
}

export { LINE_H, PAD_Y, TAG_H };
