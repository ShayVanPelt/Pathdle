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
/** Minimum clear space between pill edges (display-only). */
const GAP = 18;
const SEPARATION_ITERS = 160;
const SEPARATION_STRENGTH = 0.55;

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

type Polar = { id: string; angle: number; preferR: number };

function overlaps(a: Rect, b: Rect, gap: number) {
  return (
    a.x < b.x + b.w + gap &&
    a.x + a.w + gap > b.x &&
    a.y < b.y + b.h + gap &&
    a.y + a.h + gap > b.y
  );
}

function layoutBounds(
  ids: string[],
  bodies: Map<string, NodeBody>,
  positions: Map<string, Point>,
) {
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;

  for (const id of ids) {
    const body = bodies.get(id)!;
    const point = positions.get(id)!;
    minX = Math.min(minX, point.x - body.w / 2);
    minY = Math.min(minY, point.y - body.h / 2);
    maxX = Math.max(maxX, point.x + body.w / 2);
    maxY = Math.max(maxY, point.y + body.h / 2);
  }

  return {
    minX,
    minY,
    maxX,
    maxY,
    width: maxX - minX,
    height: maxY - minY,
  };
}

/**
 * Expand only the shorter axis to make the pill envelope circular. Expansion
 * cannot introduce collisions because center-to-center separation only grows.
 */
function circularizeLayout(
  ids: string[],
  bodies: Map<string, NodeBody>,
  positions: Map<string, Point>,
  worldCx: number,
  worldCy: number,
) {
  if (ids.length === 0) return;

  const bounds = layoutBounds(ids, bodies, positions);
  const layoutCx = (bounds.minX + bounds.maxX) / 2;
  const layoutCy = (bounds.minY + bounds.maxY) / 2;

  if (bounds.width < bounds.height) {
    const scaleX = Math.min(1.3, bounds.height / Math.max(1, bounds.width));
    for (const id of ids) {
      const point = positions.get(id)!;
      positions.set(id, {
        x: layoutCx + (point.x - layoutCx) * scaleX,
        y: point.y,
      });
    }
  } else if (bounds.height < bounds.width) {
    const scaleY = Math.min(1.3, bounds.width / Math.max(1, bounds.height));
    for (const id of ids) {
      const point = positions.get(id)!;
      positions.set(id, {
        x: point.x,
        y: layoutCy + (point.y - layoutCy) * scaleY,
      });
    }
  }

  const circularBounds = layoutBounds(ids, bodies, positions);
  const offsetX = worldCx - (circularBounds.minX + circularBounds.maxX) / 2;
  const offsetY = worldCy - (circularBounds.minY + circularBounds.maxY) / 2;
  for (const id of ids) {
    const point = positions.get(id)!;
    positions.set(id, {
      x: point.x + offsetX,
      y: point.y + offsetY,
    });
  }
}

/**
 * Display-only: keep each node's API direction, pack into a filled disk,
 * then separate AABB pills until none touch.
 */
export function spreadDisplayPositions(
  nodes: PuzzleNode[],
  apiPositions: Map<string, Point>,
  _startId?: string,
  world = 1400,
): Map<string, Point> {
  const bodies = new Map(nodes.map((n) => [n.id, nodeBody(n)]));
  const cx = world / 2;
  const cy = world / 2 + 12;
  const n = Math.max(1, nodes.length);

  let gx = 0;
  let gy = 0;
  for (const node of nodes) {
    const p = apiPositions.get(node.id)!;
    gx += p.x;
    gy += p.y;
  }
  gx /= n;
  gy /= n;

  let maxDist = 1;
  for (const node of nodes) {
    const p = apiPositions.get(node.id)!;
    maxDist = Math.max(maxDist, Math.hypot(p.x - gx, p.y - gy));
  }

  // Sized so ~60 pills with gaps still fit a filled circle inside the world.
  const disk = Math.min(world * 0.42, 420);
  const innerR = disk * 0.12;
  const outerR = disk * 1.18;

  const polars: Polar[] = nodes.map((node) => {
    const p = apiPositions.get(node.id)!;
    const dx = p.x - gx;
    const dy = p.y - gy;
    const dist = Math.hypot(dx, dy);
    const angle = dist < 2 ? hashAngle(node.id) : Math.atan2(dy, dx);
    // Sqrt map → denser fill toward the rim while keeping a filled disk.
    const t = Math.sqrt(Math.min(1, dist / maxDist));
    let preferR = innerR + t * (outerR - innerR);
    if (node.kind === "start") preferR = innerR + (outerR - innerR) * 0.22;
    if (node.kind === "target") preferR = outerR * 0.92;
    return { id: node.id, angle: wrapAngle(angle), preferR };
  });

  // Even angular nudge so nearby API angles do not start stacked.
  polars.sort((a, b) => a.angle - b.angle);
  const evenSpan = (Math.PI * 2) / polars.length;
  const evenOffset =
    polars.reduce((s, p) => s + p.angle, 0) / polars.length -
    ((polars.length - 1) * evenSpan) / 2;
  for (let i = 0; i < polars.length; i++) {
    const even = wrapAngle(evenOffset + i * evenSpan);
    const orig = polars[i].angle;
    let delta = even - orig;
    if (delta > Math.PI) delta -= Math.PI * 2;
    if (delta < -Math.PI) delta += Math.PI * 2;
    polars[i].angle = wrapAngle(orig + delta * 0.55);
  }

  const current = new Map<string, Point>();
  const prefer = new Map<string, number>();
  for (const polar of polars) {
    prefer.set(polar.id, polar.preferR);
    current.set(polar.id, {
      x: cx + Math.cos(polar.angle) * polar.preferR,
      y: cy + Math.sin(polar.angle) * polar.preferR,
    });
  }

  const ids = nodes.map((node) => node.id);
  const margin = 16;

  for (let iter = 0; iter < SEPARATION_ITERS; iter++) {
    let moved = false;

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

        moved = true;
        // Resolve along the shallower axis so pills slide past each other.
        if (ox < oy) {
          const push = ox * SEPARATION_STRENGTH;
          const dirX = Math.sign(dx || 1);
          current.set(idA, { x: pa.x - dirX * push, y: pa.y });
          current.set(idB, { x: pb.x + dirX * push, y: pb.y });
        } else {
          const push = oy * SEPARATION_STRENGTH;
          const dirY = Math.sign(dy || 1);
          current.set(idA, { x: pa.x, y: pa.y - dirY * push });
          current.set(idB, { x: pb.x, y: pb.y + dirY * push });
        }
      }
    }

    // Soft pull toward preferred radius — never hard-clamp (that re-stacks pills).
    const radialBlend = iter < SEPARATION_ITERS * 0.7 ? 0.08 : 0.02;
    for (const id of ids) {
      const body = bodies.get(id)!;
      const p = current.get(id)!;
      const vx = p.x - cx;
      const vy = p.y - cy;
      const dist = Math.hypot(vx, vy) || 1;
      const targetR = prefer.get(id)!;
      const nextR = dist + (targetR - dist) * radialBlend;
      let nx = cx + (vx / dist) * nextR;
      let ny = cy + (vy / dist) * nextR;
      nx = Math.min(world - body.w / 2 - margin, Math.max(body.w / 2 + margin, nx));
      ny = Math.min(world - body.h / 2 - margin, Math.max(body.h / 2 + margin, ny));
      if (nx !== p.x || ny !== p.y) moved = true;
      current.set(id, { x: nx, y: ny });
    }

    if (!moved && iter > 20) break;
  }

  // Final hard pass: if anything still overlaps, push purely apart (no radial pull).
  for (let iter = 0; iter < 80; iter++) {
    let hit = false;
    for (let i = 0; i < ids.length; i++) {
      for (let j = i + 1; j < ids.length; j++) {
        const idA = ids[i];
        const idB = ids[j];
        const a = bodies.get(idA)!;
        const b = bodies.get(idB)!;
        const pa = current.get(idA)!;
        const pb = current.get(idB)!;
        const ra = rectFromCenter(pa.x, pa.y, a.w, a.h);
        const rb = rectFromCenter(pb.x, pb.y, b.w, b.h);
        if (!overlaps(ra, rb, GAP)) continue;
        hit = true;
        const dx = pb.x - pa.x;
        const dy = pb.y - pa.y;
        const ox = (a.w + b.w) / 2 + GAP - Math.abs(dx);
        const oy = (a.h + b.h) / 2 + GAP - Math.abs(dy);
        if (ox < oy) {
          const push = Math.max(ox * 0.6, 1.5);
          const dirX = Math.sign(dx || 1);
          current.set(idA, {
            x: Math.min(
              world - a.w / 2 - margin,
              Math.max(a.w / 2 + margin, pa.x - dirX * push),
            ),
            y: pa.y,
          });
          current.set(idB, {
            x: Math.min(
              world - b.w / 2 - margin,
              Math.max(b.w / 2 + margin, pb.x + dirX * push),
            ),
            y: pb.y,
          });
        } else {
          const push = Math.max(oy * 0.6, 1.5);
          const dirY = Math.sign(dy || 1);
          current.set(idA, {
            x: pa.x,
            y: Math.min(
              world - a.h / 2 - margin,
              Math.max(a.h / 2 + margin, pa.y - dirY * push),
            ),
          });
          current.set(idB, {
            x: pb.x,
            y: Math.min(
              world - b.h / 2 - margin,
              Math.max(b.h / 2 + margin, pb.y + dirY * push),
            ),
          });
        }
      }
    }
    if (!hit) break;
  }

  circularizeLayout(ids, bodies, current, cx, cy);

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
