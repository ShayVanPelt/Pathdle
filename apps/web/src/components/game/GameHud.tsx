"use client";

import { useEffect, useRef, useState } from "react";

type Props = {
  brand: string;
  connectionCount: number;
  score: number;
  path: string[];
  reachedTarget: boolean;
  status: string;
  onComplete: () => void;
  onOpenHelp: () => void;
  completing?: boolean;
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
      className={`pathdle-score-value font-[family-name:var(--font-display)] text-2xl leading-none text-[var(--accent)] sm:text-[1.65rem] ${
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
  path,
  reachedTarget,
  status,
  onComplete,
  onOpenHelp,
  completing,
}: Props) {
  const pathTitles =
    path.length <= 1
      ? null
      : path.map((id) => id.replaceAll("_", " "));

  return (
    <>
      {/* Top-left brand */}
      <header className="pointer-events-none absolute left-0 top-0 z-20 flex items-start gap-3 p-4 sm:p-5">
        <div className="pointer-events-auto">
          <div className="flex items-baseline gap-3">
            <h1 className="font-[family-name:var(--font-display)] text-[1.85rem] leading-none tracking-tight text-[var(--ink-bright)] sm:text-[2.15rem]">
              {brand}
            </h1>
            <button
              type="button"
              onClick={onOpenHelp}
              className="text-[0.7rem] font-medium uppercase tracking-[0.14em] text-[var(--ink-muted)] transition hover:text-[var(--ink-bright)]"
            >
              How to play
            </button>
          </div>
        </div>
      </header>

      {/* Top-right stats */}
      <div className="pointer-events-none absolute right-0 top-0 z-20 flex items-start gap-3 p-4 sm:gap-4 sm:p-5">
        <div className="pointer-events-auto text-right">
          <p className="text-[0.6rem] uppercase tracking-[0.2em] text-[var(--ink-dim)]">
            Score
          </p>
          <AnimatedScore value={score} />
        </div>
        <div className="pointer-events-auto text-right">
          <p className="text-[0.6rem] uppercase tracking-[0.2em] text-[var(--ink-dim)]">
            Links
          </p>
          <p className="font-[family-name:var(--font-display)] text-2xl leading-none text-[var(--ink-bright)] sm:text-[1.65rem]">
            {connectionCount}
          </p>
        </div>
      </div>

      {/* Bottom path + hint */}
      <footer className="pointer-events-none absolute inset-x-0 bottom-0 z-20 flex flex-col gap-3 p-4 sm:flex-row sm:items-end sm:justify-between sm:p-5">
        <div className="pointer-events-auto max-w-md sm:max-w-lg">
          {pathTitles ? (
            <div>
              <p className="text-[0.58rem] uppercase tracking-[0.18em] text-[var(--ink-dim)]">
                Current path
              </p>
              <p className="mt-1 text-[0.8rem] leading-relaxed text-[var(--ink-muted)] sm:text-[0.85rem]">
                {pathTitles.map((title, i) => (
                  <span key={`${title}-${i}`}>
                    {i > 0 && (
                      <span className="mx-1.5 text-[var(--accent-soft)]">→</span>
                    )}
                    <span className="text-[var(--ink-bright)]">{title}</span>
                  </span>
                ))}
              </p>
            </div>
          ) : (
            <p className="text-[0.8rem] text-[var(--ink-dim)]">
              Drag empty space to pan · drag a node to connect
            </p>
          )}
        </div>

        <div className="pointer-events-auto flex flex-wrap items-center gap-3 sm:justify-end">
          {pathTitles && (
            <p className="hidden text-[0.72rem] text-[var(--ink-dim)] md:block">
              Drag empty space to pan · scroll to zoom
            </p>
          )}

          {reachedTarget && status !== "completed" && (
            <button
              type="button"
              onClick={onComplete}
              disabled={completing}
              className="rounded-md bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-[oklch(0.14_0.03_250)] transition hover:brightness-110 disabled:opacity-60"
            >
              {completing ? "Finishing…" : "See results"}
            </button>
          )}
        </div>
      </footer>
    </>
  );
}
