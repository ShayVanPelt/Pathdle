"use client";

import { useLayoutEffect, useMemo, useRef, useState } from "react";
import { articleTitle } from "@/lib/format";

type Props = {
  path: string[];
  startId: string;
  targetId: string;
  startTitle: string;
  targetTitle: string;
  titles?: Record<string, string>;
  selectedId: string | null;
  reachedTarget: boolean;
  onSelect: (id: string) => void;
};

type RailItem =
  | { kind: "hop"; id: string; pathIndex: number }
  | { kind: "ellipsis" };

function Hop({
  id,
  showArrow,
  label,
  isStart,
  isTarget,
  selected,
  muted,
  onSelect,
}: {
  id: string;
  showArrow: boolean;
  label: string;
  isStart?: boolean;
  isTarget?: boolean;
  selected: boolean;
  muted?: boolean;
  onSelect: (id: string) => void;
}) {
  return (
    <span className="flex shrink-0 items-center gap-1.5">
      {showArrow && (
        <span className="text-base text-[var(--accent-soft)]" aria-hidden>
          →
        </span>
      )}
      <button
        type="button"
        onClick={() => onSelect(id)}
        className={`max-w-[6.5rem] truncate rounded-full px-2 py-1 text-[0.8rem] font-semibold leading-none sm:max-w-[11rem] sm:px-2.5 sm:text-[0.98rem] ${
          selected
            ? "bg-[oklch(0.2_0.04_80_/_0.85)] text-[var(--focus)] ring-1 ring-[var(--accent)]"
            : isStart
              ? "text-[var(--accent)]"
              : isTarget
                ? muted
                  ? "text-[var(--ink-dim)]"
                  : "text-[var(--target)]"
                : "text-[var(--ink-bright)] hover:bg-[oklch(0.2_0.03_275_/_0.55)]"
        }`}
      >
        {isStart && (
          <span className="mr-1 hidden text-[0.58rem] font-bold uppercase tracking-[0.16em] text-[var(--accent)] sm:inline">
            Start
          </span>
        )}
        {isTarget && (
          <span className="mr-1 hidden text-[0.58rem] font-bold uppercase tracking-[0.16em] text-[var(--target)] sm:inline">
            Target
          </span>
        )}
        {label}
      </button>
    </span>
  );
}

/** Keep start + a growing tail. Middle becomes ··· when collapsed. */
function buildCollapsedItems(hops: string[], tailCount: number): RailItem[] {
  if (hops.length === 0) return [];
  if (tailCount >= hops.length - 1) {
    return hops.map((id, pathIndex) => ({ kind: "hop", id, pathIndex }));
  }

  const safeTail = Math.max(1, Math.min(tailCount, hops.length - 1));
  const tailStart = hops.length - safeTail;
  const items: RailItem[] = [{ kind: "hop", id: hops[0], pathIndex: 0 }];
  if (tailStart > 1) {
    items.push({ kind: "ellipsis" });
  }
  for (let i = tailStart; i < hops.length; i++) {
    items.push({ kind: "hop", id: hops[i], pathIndex: i });
  }
  return items;
}

export function ChartRail({
  path,
  startId,
  targetId,
  startTitle,
  targetTitle,
  titles,
  selectedId,
  reachedTarget,
  onSelect,
}: Props) {
  const hops = useMemo(
    () => (path.length > 0 ? path : [startId]),
    [path, startId],
  );
  const showPendingTarget = !hops.includes(targetId);

  const plateRef = useRef<HTMLDivElement>(null);
  const [tailCount, setTailCount] = useState(hops.length);

  const items = useMemo(
    () => buildCollapsedItems(hops, tailCount),
    [hops, tailCount],
  );

  // Prefer full path on hop / slot-size changes; collapse effect trims the middle.
  useLayoutEffect(() => {
    setTailCount(hops.length);
    const plate = plateRef.current;
    const slot = plate?.parentElement;
    if (!slot) return;
    const ro = new ResizeObserver(() => setTailCount(hops.length));
    ro.observe(slot);
    return () => ro.disconnect();
  }, [hops]);

  useLayoutEffect(() => {
    const plate = plateRef.current;
    const slot = plate?.parentElement;
    if (!plate || !slot) return;
    if (plate.scrollWidth <= slot.clientWidth + 1) return;
    if (tailCount <= 1) return;
    setTailCount((t) => Math.max(1, t - 1));
  }, [items, tailCount]);

  return (
    <div
      ref={plateRef}
      className="pointer-events-auto pathdle-instrument flex h-full w-full max-w-full min-w-0 items-center justify-center gap-2 rounded-2xl px-2.5 py-2 sm:w-fit sm:gap-4 sm:px-5 sm:py-3"
    >
      <p className="hidden shrink-0 text-[0.58rem] font-semibold uppercase tracking-[0.2em] text-[var(--ink-dim)] sm:block">
        Path
      </p>
      <div className="pathdle-chart-rail min-w-0 max-w-full overflow-hidden">
        <div className="flex h-full max-w-full items-center gap-1 sm:gap-1.5">
          {items.map((item, i) => {
            if (item.kind === "ellipsis") {
              return (
                <span
                  key={`e-${i}`}
                  className="flex shrink-0 items-center gap-1.5"
                >
                  {i > 0 && (
                    <span
                      className="text-base text-[var(--accent-soft)]"
                      aria-hidden
                    >
                      →
                    </span>
                  )}
                  <span
                    className="shrink-0 px-1 text-base text-[var(--ink-dim)]"
                    aria-hidden
                  >
                    ···
                  </span>
                </span>
              );
            }

            const { id, pathIndex } = item;
            const isStart = id === startId;
            const isTarget = id === targetId;
            const label = isStart
              ? startTitle
              : isTarget
                ? targetTitle
                : articleTitle(id, titles);

            return (
              <Hop
                key={`${id}-${pathIndex}`}
                id={id}
                showArrow={i > 0}
                label={label}
                isStart={isStart}
                isTarget={isTarget}
                selected={selectedId === id}
                onSelect={onSelect}
              />
            );
          })}
          {showPendingTarget && (
            <>
              <span className="flex shrink-0 items-center gap-1.5" aria-hidden>
                <span className="text-base text-[var(--accent-soft)]">→</span>
                <span className="shrink-0 px-1 text-base text-[var(--ink-dim)]">
                  ···
                </span>
              </span>
              <Hop
                id={targetId}
                showArrow
                label={targetTitle}
                isTarget
                selected={selectedId === targetId}
                muted={!reachedTarget}
                onSelect={onSelect}
              />
            </>
          )}
        </div>
      </div>
    </div>
  );
}
