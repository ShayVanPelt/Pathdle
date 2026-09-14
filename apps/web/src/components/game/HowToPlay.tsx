"use client";

import { SCORING } from "@/lib/api/types";

type Props = {
  open: boolean;
  onClose: () => void;
};

const STEPS = [
  {
    title: "Goal",
    body: "Get from START to TARGET. Links are one-way. Lower score wins.",
  },
  {
    title: `Test a link (+${SCORING.successfulLink} or +${SCORING.failedLink})`,
    body: `Drag from one node to another. A real link stays forever (+${SCORING.successfulLink}). A miss costs +${SCORING.failedLink}. No undoing links.`,
  },
  {
    title: `Reveal hints (+${SCORING.revealOutbound})`,
    body: `Click a node (don't drag) → Reveal hints. Dashed lines are hints only — drag to confirm a path link.`,
  },
  {
    title: "Explore the map",
    body: "Drag empty space to pan. Scroll to zoom.",
  },
] as const;

export function HowToPlay({ open, onClose }: Props) {
  if (!open) return null;

  return (
    <div
      className="absolute inset-0 z-40 flex items-center justify-center bg-[oklch(0.08_0.03_250_/_0.72)] p-5 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="howto-title"
    >
      <div className="pathdle-hud-chip w-full max-w-md rounded-xl p-6 shadow-2xl sm:p-8">
        <p className="text-[0.65rem] uppercase tracking-[0.2em] text-[var(--accent)]">
          How to play
        </p>
        <h2
          id="howto-title"
          className="mt-2 font-[family-name:var(--font-display)] text-3xl text-[var(--ink-bright)]"
        >
          Pathdle
        </h2>
        <p className="mt-2 text-sm leading-relaxed text-[var(--ink-muted)]">
          Chart a path through a hidden knowledge network. Points are a cost —
          guess carefully.
        </p>

        <ol className="mt-6 space-y-4">
          {STEPS.map((step, index) => (
            <li key={step.title} className="flex gap-3">
              <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[var(--accent)] text-xs font-semibold text-[oklch(0.14_0.03_250)]">
                {index + 1}
              </span>
              <div>
                <p className="text-sm font-semibold text-[var(--ink-bright)]">
                  {step.title}
                </p>
                <p className="mt-0.5 text-sm leading-relaxed text-[var(--ink-muted)]">
                  {step.body}
                </p>
              </div>
            </li>
          ))}
        </ol>

        <button
          type="button"
          onClick={onClose}
          className="mt-8 w-full rounded-md bg-[var(--accent)] px-4 py-3 text-sm font-semibold text-[oklch(0.14_0.03_250)] transition hover:brightness-110"
        >
          Got it — start charting
        </button>
      </div>
    </div>
  );
}
