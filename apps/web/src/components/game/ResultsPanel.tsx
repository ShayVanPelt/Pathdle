"use client";

import type { CompleteResponse } from "@/lib/api/types";

type Props = {
  result: CompleteResponse;
  onClose: () => void;
};

function formatPath(path: string[]) {
  return path.map((id) => id.replaceAll("_", " ")).join(" → ");
}

export function ResultsPanel({ result, onClose }: Props) {
  return (
    <div className="absolute inset-0 z-30 flex items-center justify-center bg-[oklch(0.08_0.03_250_/_0.68)] p-6 backdrop-blur-sm">
      <div
        role="dialog"
        aria-labelledby="results-title"
        className="pathdle-hud-chip w-full max-w-lg rounded-xl p-6 shadow-2xl sm:p-8"
      >
        <p className="text-[0.65rem] uppercase tracking-[0.2em] text-[var(--ink-muted)]">
          Voyage complete
        </p>
        <h2
          id="results-title"
          className="mt-2 font-[family-name:var(--font-display)] text-3xl text-[var(--ink-bright)]"
        >
          Efficiency {Math.round(result.efficiency * 100)}%
        </h2>
        <p className="mt-2 text-sm text-[var(--ink-muted)]">
          You used {result.playerConnectionCount} connections vs optimal{" "}
          {result.optimalLength}. Score {result.score}.
        </p>

        <div className="mt-6 space-y-4 text-sm">
          <div>
            <p className="text-[0.65rem] uppercase tracking-[0.18em] text-[var(--accent)]">
              Your path
            </p>
            <p className="mt-1 leading-relaxed text-[var(--ink-bright)]">
              {formatPath(result.playerPath)}
            </p>
          </div>
          <div>
            <p className="text-[0.65rem] uppercase tracking-[0.18em] text-[var(--target)]">
              Optimal path
            </p>
            <p className="mt-1 leading-relaxed text-[var(--ink-bright)]">
              {formatPath(result.optimalPath)}
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={onClose}
          className="mt-8 w-full rounded-md border border-[var(--hud-border)] px-4 py-3 text-sm text-[var(--ink-bright)] transition hover:bg-[oklch(0.2_0.03_250_/_0.6)]"
        >
          Keep exploring
        </button>
      </div>
    </div>
  );
}
