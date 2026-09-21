import type { NodeKind, PuzzleNode } from "@/lib/api/types";

export type Point = { x: number; y: number };

export type Rect = {
  id?: string;
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

export type LinkGeom = {
  d: string;
  fromRim: Point;
  toRim: Point;
  p0: Point;
  ctrl: Point;
  p1: Point;
};

export type GroupSwatch = {
  hue: number;
  fill: string;
  stroke: string;
  text: string;
  dock: string;
  halo: string;
};

export type GroupPlaque = {
  key: string;
  x: number;
  y: number;
  w: number;
  h: number;
  lines: string[];
  tick: Point | null;
  swatch: GroupSwatch;
};

const CHAR_W = 8.6;
const LINE_H = 18;
const PAD_X = 18;
const PAD_Y = 14;
const TAG_H = 20;
const MAX_LINE_CHARS = 15;
const MAX_LINES = 3;
/** Minimum clear space between pill edges (display-only). */
const GAP = 22;
const SEPARATION_ITERS = 180;
const SEPARATION_STRENGTH = 0.58;
const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5));
const PLAQUE_CHAR_W = 6.7;
const PLAQUE_LINE_H = 14;

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
  const extra = hasTag ? 22 : 0;
  const w = Math.min(272, Math.max(hasTag ? 158 : 108, maxChars * CHAR_W + PAD_X * 2 + extra));
  const h = PAD_Y * 2 + lines.length * LINE_H + (hasTag ? TAG_H + 8 : 0);
  return { w, h, r: Math.min(hasTag ? 28 : 24, h / 2), lines, hasTag };
}

export function nodeHitRadius(node: PuzzleNode) {
  const b = nodeBody(node);
  return Math.max(b.w, b.h) / 2 + 10;
}

export function nodeDisplayRadius(kind: NodeKind) {
  if (kind === "start" || kind === "target") return 52;
  return 40;
}

function rectFromCenter(
  cx: number,
  cy: number,
  w: number,
  h: number,
  id?: string,
): Rect {
  return { id, cx, cy, w, h, x: cx - w / 2, y: cy - h / 2 };
}

function overlaps(a: Rect, b: Rect, gap: number) {
  return (
    a.x < b.x + b.w + gap &&
    a.x + a.w + gap > b.x &&
    a.y < b.y + b.h + gap &&
    a.y + a.h + gap > b.y
  );
}

function pointInRect(p: Point, rect: Rect, pad = 0) {
  return (
    p.x >= rect.x - pad &&
    p.x <= rect.x + rect.w + pad &&
    p.y >= rect.y - pad &&
    p.y <= rect.y + rect.h + pad
  );
}

function hash01(id: string) {
  let h = 2166136261;
  for (let i = 0; i < id.length; i++) {
    h ^= id.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0) / 4294967296;
}

function wrapAngle(a: number) {
  const tau = Math.PI * 2;
  return ((a % tau) + tau) % tau;
}

function lerpAngle(a: number, b: number, t: number) {
  let delta = b - a;
  if (delta > Math.PI) delta -= Math.PI * 2;
  if (delta < -Math.PI) delta += Math.PI * 2;
  return wrapAngle(a + delta * t);
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

function clampNode(
  id: string,
  point: Point,
  bodies: Map<string, NodeBody>,
  world: number,
  margin: number,
): Point {
  const body = bodies.get(id)!;
  return {
    x: Math.min(world - body.w / 2 - margin, Math.max(body.w / 2 + margin, point.x)),
    y: Math.min(world - body.h / 2 - margin, Math.max(body.h / 2 + margin, point.y)),
  };
}

/**
 * Display-only constellation: START and TARGET sit on opposite sides of a
 * filled field. Every other star is scattered through the interior with a
 * hashed sunflower + jitter — path order is not preserved, so the hop before
 * TARGET is not glued to it. API positions are a faint directional spice only.
 */
export function spreadDisplayPositions(
  nodes: PuzzleNode[],
  apiPositions: Map<string, Point>,
  startId?: string,
  targetId?: string,
  world = 1400,
): Map<string, Point> {
  const bodies = new Map(nodes.map((n) => [n.id, nodeBody(n)]));
  const cx = world / 2;
  const cy = world / 2 + 10;
  const ids = nodes.map((node) => node.id);
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

  const fieldRx = world * 0.42;
  const fieldRy = world * 0.39;
  const homes = new Map<string, Point>();
  const current = new Map<string, Point>();

  const axis = hash01(`${startId ?? ""}|${targetId ?? ""}|axis`) * Math.PI * 2;
  const opposeJitter =
    (hash01(`${startId ?? ""}|${targetId ?? ""}|opp`) - 0.5) * 0.34;

  if (startId && bodies.has(startId)) {
    const r = 0.72 + hash01(`${startId}|sr`) * 0.14;
    homes.set(startId, {
      x: cx + Math.cos(axis) * fieldRx * r,
      y: cy + Math.sin(axis) * fieldRy * r,
    });
  }
  if (targetId && bodies.has(targetId)) {
    const r = 0.74 + hash01(`${targetId}|tr`) * 0.14;
    const ang = axis + Math.PI + opposeJitter;
    homes.set(targetId, {
      x: cx + Math.cos(ang) * fieldRx * r,
      y: cy + Math.sin(ang) * fieldRy * r,
    });
  }

  const others = nodes
    .filter((node) => node.id !== startId && node.id !== targetId)
    .sort((a, b) => hash01(a.id) - hash01(b.id));

  const count = Math.max(1, others.length);
  for (let i = 0; i < others.length; i++) {
    const node = others[i];
    const api = apiPositions.get(node.id)!;
    const apiAngle = Math.atan2(api.y - gy, api.x - gx);
    const t = (i + 0.12) / count;
    // Area-uniform disk — the interior is occupied, not a hollow ring.
    const radial = Math.sqrt(t) * (0.88 + hash01(`${node.id}|rad`) * 0.14);
    const swirl = i * GOLDEN_ANGLE + hash01(node.id) * 1.15;
    const angle = lerpAngle(swirl, apiAngle, 0.16);
    const wobble = (hash01(`${node.id}|w`) - 0.5) * 0.09;
    homes.set(node.id, {
      x: cx + Math.cos(angle) * fieldRx * Math.min(0.98, radial + wobble),
      y: cy + Math.sin(angle) * fieldRy * Math.min(0.98, radial + wobble * 0.85),
    });
  }

  const margin = 18;
  for (const id of ids) {
    const home = homes.get(id) ?? { x: cx, y: cy };
    current.set(id, clampNode(id, home, bodies, world, margin));
  }

  const pole = new Set([startId, targetId].filter(Boolean) as string[]);

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
        const massA = pole.has(idA) ? 0.28 : 1;
        const massB = pole.has(idB) ? 0.28 : 1;
        const inv = 1 / (massA + massB);
        if (ox < oy) {
          const push = ox * SEPARATION_STRENGTH;
          const dirX = Math.sign(dx || 1);
          current.set(idA, { x: pa.x - dirX * push * massB * inv * 2, y: pa.y });
          current.set(idB, { x: pb.x + dirX * push * massA * inv * 2, y: pb.y });
        } else {
          const push = oy * SEPARATION_STRENGTH;
          const dirY = Math.sign(dy || 1);
          current.set(idA, { x: pa.x, y: pa.y - dirY * push * massB * inv * 2 });
          current.set(idB, { x: pb.x, y: pb.y + dirY * push * massA * inv * 2 });
        }
      }
    }

    const homeBlend = iter < SEPARATION_ITERS * 0.65 ? 0.07 : 0.018;
    for (const id of ids) {
      const p = current.get(id)!;
      const home = homes.get(id)!;
      const blend = pole.has(id) ? homeBlend * 1.6 : homeBlend;
      const next = clampNode(
        id,
        {
          x: p.x + (home.x - p.x) * blend,
          y: p.y + (home.y - p.y) * blend,
        },
        bodies,
        world,
        margin,
      );
      if (next.x !== p.x || next.y !== p.y) moved = true;
      current.set(id, next);
    }

    if (!moved && iter > 24) break;
  }

  for (let iter = 0; iter < 90; iter++) {
    let hit = false;
    for (let i = 0; i < ids.length; i++) {
      for (let j = i + 1; j < ids.length; j++) {
        const idA = ids[i];
        const idB = ids[j];
        const a = bodies.get(idA)!;
        const b = bodies.get(idB)!;
        const pa = current.get(idA)!;
        const pb = current.get(idB)!;
        const ra = rectFromCenter(pa.x, pa.y, a.w, a.h, idA);
        const rb = rectFromCenter(pb.x, pb.y, b.w, b.h, idB);
        if (!overlaps(ra, rb, GAP)) continue;
        hit = true;
        const dx = pb.x - pa.x;
        const dy = pb.y - pa.y;
        const ox = (a.w + b.w) / 2 + GAP - Math.abs(dx);
        const oy = (a.h + b.h) / 2 + GAP - Math.abs(dy);
        const massA = pole.has(idA) ? 0.28 : 1;
        const massB = pole.has(idB) ? 0.28 : 1;
        const inv = 1 / (massA + massB);
        if (ox < oy) {
          const push = Math.max(ox * 0.62, 1.6);
          const dirX = Math.sign(dx || 1);
          current.set(
            idA,
            clampNode(
              idA,
              { x: pa.x - dirX * push * massB * inv * 2, y: pa.y },
              bodies,
              world,
              margin,
            ),
          );
          current.set(
            idB,
            clampNode(
              idB,
              { x: pb.x + dirX * push * massA * inv * 2, y: pb.y },
              bodies,
              world,
              margin,
            ),
          );
        } else {
          const push = Math.max(oy * 0.62, 1.6);
          const dirY = Math.sign(dy || 1);
          current.set(
            idA,
            clampNode(
              idA,
              { x: pa.x, y: pa.y - dirY * push * massB * inv * 2 },
              bodies,
              world,
              margin,
            ),
          );
          current.set(
            idB,
            clampNode(
              idB,
              { x: pb.x, y: pb.y + dirY * push * massA * inv * 2 },
              bodies,
              world,
              margin,
            ),
          );
        }
      }
    }
    if (!hit) break;
  }

  fitConstellation(ids, bodies, current, world, cx, cy);
  return current;
}

function fitConstellation(
  ids: string[],
  bodies: Map<string, NodeBody>,
  positions: Map<string, Point>,
  world: number,
  worldCx: number,
  worldCy: number,
) {
  if (ids.length === 0) return;
  const bounds = layoutBounds(ids, bodies, positions);
  const pad = 70;
  const usable = world - pad * 2;
  const scale = Math.min(
    1.22,
    usable / Math.max(1, bounds.width),
    usable / Math.max(1, bounds.height),
  );
  const layoutCx = (bounds.minX + bounds.maxX) / 2;
  const layoutCy = (bounds.minY + bounds.maxY) / 2;
  for (const id of ids) {
    const point = positions.get(id)!;
    positions.set(
      id,
      clampNode(
        id,
        {
          x: worldCx + (point.x - layoutCx) * scale,
          y: worldCy + (point.y - layoutCy) * scale,
        },
        bodies,
        world,
        16,
      ),
    );
  }
}

export function nodeRects(
  nodes: PuzzleNode[],
  positions: Map<string, Point>,
): Rect[] {
  return nodes.map((node) => {
    const p = positions.get(node.id)!;
    const b = nodeBody(node);
    return rectFromCenter(p.x, p.y, b.w, b.h, node.id);
  });
}

/** Intersection of center→toward with the pill AABB, slightly outside the stroke. */
export function rimPoint(center: Point, body: NodeBody, toward: Point): Point {
  const dx = toward.x - center.x;
  const dy = toward.y - center.y;
  if (Math.abs(dx) < 1e-4 && Math.abs(dy) < 1e-4) {
    return { x: center.x + body.w / 2, y: center.y };
  }
  const hx = body.w / 2 + 1.5;
  const hy = body.h / 2 + 1.5;
  const sx = hx / Math.max(1e-4, Math.abs(dx));
  const sy = hy / Math.max(1e-4, Math.abs(dy));
  const s = Math.min(sx, sy);
  return { x: center.x + dx * s, y: center.y + dy * s };
}

export function quadPoint(p0: Point, ctrl: Point, p1: Point, t: number): Point {
  const u = 1 - t;
  return {
    x: u * u * p0.x + 2 * u * t * ctrl.x + t * t * p1.x,
    y: u * u * p0.y + 2 * u * t * ctrl.y + t * t * p1.y,
  };
}

function curveHitsOtherNodes(
  p0: Point,
  ctrl: Point,
  p1: Point,
  obstacles: Rect[],
  skip: Set<string>,
) {
  let hits = 0;
  for (let i = 2; i <= 10; i++) {
    const p = quadPoint(p0, ctrl, p1, i / 12);
    for (const rect of obstacles) {
      if (rect.id && skip.has(rect.id)) continue;
      if (pointInRect(p, rect, 6)) {
        hits += 1;
        break;
      }
    }
  }
  return hits;
}

/**
 * Rim-to-rim constellation arc that bends around foreign pills so the stroke
 * never vanishes under an unrelated node.
 */
export function routeLink(
  fromId: string,
  toId: string,
  from: Point,
  to: Point,
  fromBody: NodeBody,
  toBody: NodeBody,
  obstacles: Rect[],
): LinkGeom {
  const p0 = rimPoint(from, fromBody, to);
  const p1 = rimPoint(to, toBody, from);
  const dx = p1.x - p0.x;
  const dy = p1.y - p0.y;
  const len = Math.hypot(dx, dy) || 1;
  const nx = -dy / len;
  const ny = dx / len;
  const skip = new Set([fromId, toId]);
  const key = fromId <= toId ? `${fromId}|${toId}` : `${toId}|${fromId}`;
  const prefer = hash01(key) > 0.5 ? 1 : -1;
  const base = Math.min(110, Math.max(32, len * 0.16));

  let bestCtrl = { x: (p0.x + p1.x) / 2 + nx * base * prefer, y: (p0.y + p1.y) / 2 + ny * base * prefer };
  let bestHits = Infinity;

  for (const side of [prefer, -prefer]) {
    for (const scale of [1, 1.45, 1.95, 2.5, 3.1]) {
      const bulge = base * scale * side;
      const ctrl = {
        x: (p0.x + p1.x) / 2 + nx * bulge,
        y: (p0.y + p1.y) / 2 + ny * bulge,
      };
      const hits = curveHitsOtherNodes(p0, ctrl, p1, obstacles, skip);
      if (hits < bestHits || (hits === bestHits && scale < 1.5)) {
        bestHits = hits;
        bestCtrl = ctrl;
      }
      if (hits === 0 && scale <= 1.45) break;
    }
    if (bestHits === 0) break;
  }

  return {
    d: `M ${p0.x} ${p0.y} Q ${bestCtrl.x} ${bestCtrl.y} ${p1.x} ${p1.y}`,
    fromRim: p0,
    toRim: p1,
    p0,
    ctrl: bestCtrl,
    p1,
  };
}

function inflateRect(rect: Rect, pad: number): Rect {
  return {
    ...rect,
    x: rect.x - pad,
    y: rect.y - pad,
    w: rect.w + pad * 2,
    h: rect.h + pad * 2,
  };
}

/** Shared rose for every relationship plaque — groups read as one legend family. */
const GROUP_HUE = 330;

export function groupSwatch(_label?: string): GroupSwatch {
  const hue = GROUP_HUE;
  return {
    hue,
    fill: `oklch(0.13 0.055 ${hue} / 0.96)`,
    stroke: `oklch(0.84 0.11 ${hue} / 0.88)`,
    text: `oklch(0.93 0.07 ${hue})`,
    dock: `oklch(0.82 0.12 ${hue})`,
    halo: `oklch(0.72 0.1 ${hue} / 0.42)`,
  };
}

function plaqueSize(lines: string[]) {
  const maxChars = Math.max(...lines.map((l) => l.length), 4);
  const w = Math.min(188, Math.max(80, maxChars * PLAQUE_CHAR_W + 24));
  const h = 11 + lines.length * PLAQUE_LINE_H;
  return { w, h };
}

function overlapCost(rect: Rect, others: Rect[], gap: number, weight: number) {
  let cost = 0;
  for (const other of others) {
    if (overlaps(rect, other, gap)) cost += weight;
  }
  return cost;
}

export function placeGroupPlaques(
  items: { key: string; label: string; fromId: string; toId: string; geom: LinkGeom }[],
  obstacles: Rect[],
): GroupPlaque[] {
  const nodes = obstacles.map((r) => inflateRect(r, 10));
  const plaques: Rect[] = [];
  const degree = new Map<string, number>();
  for (const item of items) {
    degree.set(item.fromId, (degree.get(item.fromId) ?? 0) + 1);
    degree.set(item.toId, (degree.get(item.toId) ?? 0) + 1);
  }

  const ranked = [...items].sort(
    (a, b) => b.label.length - a.label.length || a.key.localeCompare(b.key),
  );
  const placed: GroupPlaque[] = [];

  for (const item of ranked) {
    const lines = wrapTitle(item.label, 18, 2);
    const { w, h } = plaqueSize(lines);
    const swatch = groupSwatch(item.label);
    const { p0, ctrl, p1 } = item.geom;
    const dx = p1.x - p0.x;
    const dy = p1.y - p0.y;
    const len = Math.hypot(dx, dy) || 1;
    const nx = -dy / len;
    const ny = dx / len;
    const bulgeSide = Math.sign(
      (ctrl.x - (p0.x + p1.x) / 2) * nx + (ctrl.y - (p0.y + p1.y) / 2) * ny || 1,
    );
    const df = degree.get(item.fromId) ?? 1;
    const dt = degree.get(item.toId) ?? 1;
    const pref = dt > df ? 0.34 : df > dt ? 0.66 : 0.5;
    const short = len < 180;
    const ts = [pref, pref - 0.1, pref + 0.1, pref - 0.18, pref + 0.18, 0.24, 0.76]
      .map((t) => Math.min(0.78, Math.max(0.22, t)));
    const offsets = short
      ? [28, -28, 40, -40, 54, -54, 70, -70]
      : [18, -18, 30, -30, 0, 44, -44, 60, -60];

    let best: { x: number; y: number; offset: number; along: Point; cost: number } | null =
      null;

    for (const t of ts) {
      const along = quadPoint(p0, ctrl, p1, t);
      for (const offset of offsets) {
        const x = along.x + nx * bulgeSide * offset;
        const y = along.y + ny * bulgeSide * offset;
        const rect = rectFromCenter(x, y, w, h);
        const cost =
          overlapCost(rect, nodes, 8, 900) +
          overlapCost(rect, plaques, 12, 260) +
          Math.abs(offset) * 0.35 +
          Math.abs(t - pref) * 70;
        if (!best || cost < best.cost) best = { x, y, offset, along, cost };
        if (cost < 40) break;
      }
    }

    const chosen = best ?? {
      x: (p0.x + p1.x) / 2,
      y: (p0.y + p1.y) / 2,
      offset: 28,
      along: quadPoint(p0, ctrl, p1, 0.5),
      cost: 0,
    };
    plaques.push(rectFromCenter(chosen.x, chosen.y, w, h));
    placed.push({
      key: item.key,
      x: chosen.x,
      y: chosen.y,
      w,
      h,
      lines,
      tick: Math.abs(chosen.offset) >= 22 ? chosen.along : null,
      swatch,
    });
  }

  return placed;
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
    const a = playerPath[i];
    const b = playerPath[i + 1];
    keys.add(a <= b ? `${a}|${b}` : `${b}|${a}`);
  }
  return keys;
}

export { LINE_H, PAD_Y, TAG_H, PLAQUE_LINE_H };
