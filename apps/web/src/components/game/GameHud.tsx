"use client";

import { useEffect, useRef, useState } from "react";
import { ChartRail } from "./ChartRail";

type Props = {
  brand: string;
  connectionCount: number;
  score: number;
  hintsRemaining: number;
  path: string[];
  startId: string;
  targetId: string;
  startTitle: string;
  targetTitle: string;
  titles?: Record<string, string>;
  selectedId: string | null;
  reachedTarget: boolean;
  onSelectNode: (id: string) => void;
  onOpenHelp: () => void;
};

function AnimatedScore({ value }: { value: number }) {
  const [display, setDisplay] = useState(value);
  const [bump, setBump] = useState(false);
  const displayRef = useRef(value);

  useEffect(() => {
    const from = displayRef.current;
    if (from === value) return;

    setBump(true);
    const t0 = performance.now();
    const dur = 420;
    let raf = 0;

    const tick = (now: number) => {
      const p = Math.min(1, (now - t0) / dur);
      const eased = 1 - (1 - p) ** 3;
      const next = Math.round(from + (value - from) * eased);
      displayRef.current = next;
      setDisplay(next);
      if (p < 1) {
        raf = requestAnimationFrame(tick);
      } else {
        setBump(false);
      }
    };

    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [value]);

  return (
    <p
      className={`pathdle-score-value font-[family-name:var(--font-display)] text-[1.5rem] leading-none text-[var(--accent)] sm:text-[2.15rem] ${
        bump ? "is-bump" : ""
      }`}
    >
      {display}
    </p>
  );
}

export function GameHud({
  brand,
  connectionCount,
  score,
  hintsRemaining,
  path,
  startId,
  targetId,
  startTitle,
  targetTitle,
  titles,
  selectedId,
  reachedTarget,
  onSelectNode,
  onOpenHelp,
}: Props) {
  return (
    <header className="pointer-events-none absolute inset-x-0 top-0 z-20 flex flex-wrap items-stretch gap-2 p-2.5 sm:flex-nowrap sm:gap-4 sm:p-5">
      {/* Brand */}
      <div className="pointer-events-auto pathdle-instrument flex shrink-0 items-center gap-2 self-stretch rounded-2xl px-2.5 py-2 sm:gap-4 sm:px-5 sm:py-3">
        <h1 className="font-[family-name:var(--font-display)] text-[1.35rem] leading-none tracking-tight text-[var(--ink-bright)] sm:text-[2.15rem]">
          {brand}
        </h1>
        <button
          type="button"
          onClick={onOpenHelp}
          className="flex items-center gap-2 rounded-full px-1 py-1 text-[0.72rem] font-semibold uppercase tracking-[0.16em] text-[var(--ink-muted)] transition hover:text-[var(--ink-bright)]"
          aria-label="How to play"
        >
          <span
            className="grid h-6 w-6 place-items-center rounded-full border border-[var(--hud-border)] text-[0.8rem] text-[var(--accent)]"
            aria-hidden
          >
            ?
          </span>
          <span className="hidden sm:inline">How to play</span>
        </button>
      </div>

      {/* Score (mobile: same row as brand; desktop: right column) */}
      <div className="pointer-events-auto pathdle-instrument ml-auto flex shrink-0 items-center gap-3 self-stretch rounded-2xl px-2.5 py-2 sm:order-last sm:ml-0 sm:gap-5 sm:px-5 sm:py-3">
        <div className="text-right">
          <p className="text-[0.55rem] font-semibold uppercase tracking-[0.2em] text-[var(--ink-dim)]">
            Score
          </p>
          <AnimatedScore value={score} />
        </div>
        <div className="w-px self-stretch bg-[var(--hud-border)]" aria-hidden />
        <div className="text-right">
          <p className="text-[0.55rem] font-semibold uppercase tracking-[0.2em] text-[var(--ink-dim)]">
            Links
          </p>
          <p className="font-[family-name:var(--font-display)] text-[1.5rem] leading-none text-[var(--ink-bright)] sm:text-[2.15rem]">
            {connectionCount}
          </p>
        </div>
        <div className="w-px self-stretch bg-[var(--hud-border)]" aria-hidden />
        <div className="text-right">
          <p className="text-[0.55rem] font-semibold uppercase tracking-[0.2em] text-[var(--ink-dim)]">
            Hints
          </p>
          <p className="font-[family-name:var(--font-display)] text-[1.5rem] leading-none text-[var(--hint)] sm:text-[2.15rem]">
            {hintsRemaining}
          </p>
        </div>
      </div>

      {/* Path: full-width row on mobile; flex middle on desktop */}
      <div className="order-last flex w-full min-w-0 basis-full items-stretch justify-center self-stretch sm:order-none sm:w-auto sm:flex-1 sm:basis-auto">
        <ChartRail
          path={path}
          startId={startId}
          targetId={targetId}
          startTitle={startTitle}
          targetTitle={targetTitle}
          titles={titles}
          selectedId={selectedId}
          reachedTarget={reachedTarget}
          onSelect={onSelectNode}
        />
      </div>
    </header>
  );
}
