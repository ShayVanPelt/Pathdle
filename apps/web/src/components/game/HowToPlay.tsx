"use client";

import { SCORING } from "@/lib/api/types";

type Props = {
  open: boolean;
  onClose: () => void;
};

const STEPS = [
  {
    title: "Goal",
    body: "Chart a path from the gold START star to the teal TARGET. Links are one-way. Lower score wins.",
  },
  {
    title: `Test a link (+${SCORING.successfulLink})`,
    body: `Drag from START or any charted star onto another. A real link stays as a solid gold path. A miss still costs +${SCORING.failedLink}. No undoing.`,
  },
  {
    title: `Reveal hints (+${SCORING.revealOutbound})`,
    body: "Tap START or a charted star, then reveal its outbound hints. Dashed teal lines are hints only; drag to confirm.",
  },
  {
    title: "Explore the sky",
    body: "Drag empty space to pan. Scroll, pinch, or use + / - to zoom. Recenter fits your path in view.",
  },
] as const;

export function HowToPlay({ open, onClose }: Props) {
  if (!open) return null;

  return (
    <div
      className="absolute inset-0 z-40 flex items-center justify-center bg-[oklch(0.04_0.02_275_/_0.78)] p-5 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="howto-title"
    >
      <div className="pathdle-instrument w-full max-w-lg rounded-3xl p-6 shadow-2xl sm:p-9">
        <p className="text-[0.7rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent)]">
          How to play
        </p>
        <h2
          id="howto-title"
          className="mt-2 font-[family-name:var(--font-display)] text-4xl text-[var(--ink-bright)]"
        >
          Pathdle
        </h2>
        <p className="mt-3 text-base leading-relaxed text-[var(--ink-muted)]">
          A hidden constellation of links within real Wikipedia articles. Points are a cost; guess carefully.
        </p>

        <ol className="mt-7 space-y-5">
          {STEPS.map((step, index) => (
            <li key={step.title} className="flex gap-3.5">
              <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[var(--accent)] text-sm font-semibold text-[oklch(0.12_0.03_80)]">
                {index + 1}
              </span>
              <div>
                <p className="text-lg font-semibold text-[var(--ink-bright)]">{step.title}</p>
                <p className="mt-1 text-base leading-relaxed text-[var(--ink-muted)]">
                  {step.body}
                </p>
              </div>
            </li>
          ))}
        </ol>

        <button
          type="button"
          onClick={onClose}
          className="mt-8 w-full rounded-xl bg-[var(--accent)] px-4 py-3.5 text-base font-semibold text-[oklch(0.12_0.03_80)] transition hover:brightness-110"
        >
          Got it. Start charting
        </button>
      </div>
    </div>
  );
}
