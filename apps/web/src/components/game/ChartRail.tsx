"use client";

import { articleTitle } from "@/lib/format";

type Props = {
  path: string[];
  startId: string;
  targetId: string;
  startTitle: string;
  targetTitle: string;
  selectedId: string | null;
  reachedTarget: boolean;
  onSelect: (id: string) => void;
};

function Hop({
  id,
  index,
  label,
  isStart,
  isTarget,
  selected,
  muted,
  onSelect,
}: {
  id: string;
  index: number;
  label: string;
  isStart?: boolean;
  isTarget?: boolean;
  selected: boolean;
  muted?: boolean;
  onSelect: (id: string) => void;
}) {
  return (
    <span className="flex shrink-0 items-center gap-1.5">
      {index > 0 && (
        <span className="text-base text-[var(--accent-soft)]" aria-hidden>
          →
        </span>
      )}
      <button
        type="button"
        onClick={() => onSelect(id)}
        className={`max-w-[11rem] truncate rounded-full px-2.5 py-1 text-[0.92rem] font-semibold leading-none sm:text-[0.98rem] ${
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
          <span className="mr-1 text-[0.58rem] font-bold uppercase tracking-[0.16em] text-[var(--accent)]">
            Start
          </span>
        )}
        {isTarget && (
          <span className="mr-1 text-[0.58rem] font-bold uppercase tracking-[0.16em] text-[var(--target)]">
            Target
          </span>
        )}
        {label}
      </button>
    </span>
  );
}

export function ChartRail({
  path,
  startId,
  targetId,
  startTitle,
  targetTitle,
  selectedId,
  reachedTarget,
  onSelect,
}: Props) {
  const hops = path.length > 0 ? path : [startId];
  const showEllipsis = !hops.includes(targetId);

  return (
    <div className="pointer-events-auto pathdle-instrument flex h-full w-full min-w-0 items-center gap-3 rounded-2xl px-3 py-2.5 sm:gap-4 sm:px-5 sm:py-3">
      <p className="hidden shrink-0 text-[0.58rem] font-semibold uppercase tracking-[0.2em] text-[var(--ink-dim)] sm:block">
        Path
      </p>
      <div className="pathdle-chart-rail min-w-0 flex-1 overflow-x-auto overflow-y-hidden">
        <div className="flex h-full w-max min-w-full items-center justify-center gap-1.5">
          {hops.map((id, i) => {
            const isStart = id === startId;
            const isTarget = id === targetId;
            const label = isStart ? startTitle : isTarget ? targetTitle : articleTitle(id);
            return (
              <Hop
                key={`${id}-${i}`}
                id={id}
                index={i}
                label={label}
                isStart={isStart}
                isTarget={isTarget}
                selected={selectedId === id}
                onSelect={onSelect}
              />
            );
          })}
          {showEllipsis && (
            <>
              <span className="shrink-0 px-1 text-base text-[var(--ink-dim)]" aria-hidden>
                ···
              </span>
              <Hop
                id={targetId}
                index={0}
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
