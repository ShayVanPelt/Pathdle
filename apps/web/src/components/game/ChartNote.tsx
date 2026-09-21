"use client";

import type { CSSProperties } from "react";
import { SCORING } from "@/lib/api/types";

export type ChartNeighbor = {
  id: string;
  title: string;
  kind: "path" | "hint";
};

type Props = {
  title: string;
  description?: string | null;
  neighbors: ChartNeighbor[];
  canExplore: boolean;
  revealed: boolean;
  hintsRemaining: number;
  playLocked?: boolean;
  variant: "popover" | "sheet";
  style?: CSSProperties;
  onReveal: () => void;
  onClose: () => void;
};

export function ChartNote({
  title,
  description,
  neighbors,
  canExplore,
  revealed,
  hintsRemaining,
  playLocked,
  variant,
  style,
  onReveal,
  onClose,
}: Props) {
  const canPaidReveal = !revealed && hintsRemaining > 0;
  const canFreeReshow = revealed;
  const revealDisabled =
    !canExplore || playLocked || (!canPaidReveal && !canFreeReshow);

  let revealLabel: string;
  if (!canExplore) {
    revealLabel = "Connect this star to unlock hints";
  } else if (revealed) {
    revealLabel = "Show connections again (free)";
  } else if (hintsRemaining <= 0) {
    revealLabel = "No hints remaining";
  } else {
    revealLabel = `Reveal connections  +${SCORING.revealOutbound}  ·  ${hintsRemaining} left`;
  }

  const body = (
    <>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
            Selected star
          </p>
          <p className="mt-1 text-lg font-semibold leading-snug text-[var(--ink-bright)] sm:text-xl">
            {title}
          </p>
          {description ? (
            <p className="mt-1.5 text-sm leading-snug text-[var(--ink-muted)]">{description}</p>
          ) : null}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="mt-0.5 text-sm text-[var(--ink-dim)] transition hover:text-[var(--ink-bright)]"
          aria-label="Close"
        >
          Close
        </button>
      </div>

      <p className="mt-3 text-sm leading-relaxed text-[var(--ink-muted)]">
        {canExplore
          ? "Drag to another star to test a connection. Hints show dashed lines to neighbors — not why they connect. Confirming a link clears hints from that star."
          : "Chart a confirmed path to this star before exploring from it."}
      </p>

      {neighbors.length > 0 && (
        <ul className="mt-3 max-h-32 space-y-1.5 overflow-y-auto text-base">
          {neighbors.map((n) => (
            <li key={n.id} className="flex items-center gap-2 text-[var(--ink-bright)]">
              <span
                className={`h-2 w-2 rounded-full ${
                  n.kind === "path" ? "bg-[var(--accent)]" : "bg-[var(--hint)]"
                }`}
              />
              <span>{n.title}</span>
              <span className="text-xs uppercase tracking-[0.14em] text-[var(--ink-dim)]">
                {n.kind === "path" ? "charted" : "hint"}
              </span>
            </li>
          ))}
        </ul>
      )}

      <button
        type="button"
        className="mt-4 w-full rounded-xl bg-[var(--hint)] px-3 py-3 text-base font-semibold text-[oklch(0.12_0.03_250)] transition hover:brightness-110 disabled:opacity-50"
        onClick={(event) => {
          event.stopPropagation();
          onReveal();
        }}
        disabled={revealDisabled}
      >
        {revealLabel}
      </button>
    </>
  );

  if (variant === "sheet") {
    return (
      <div
        data-node-menu
        className="pointer-events-auto absolute inset-x-3 bottom-24 z-20 pathdle-instrument rounded-2xl p-4"
      >
        {body}
      </div>
    );
  }

  return (
    <div
      data-node-menu
      className="pointer-events-auto absolute z-20 w-[min(22rem,calc(100vw-2rem))] pathdle-instrument rounded-2xl p-4 shadow-2xl"
      style={style}
    >
      {body}
    </div>
  );
}
