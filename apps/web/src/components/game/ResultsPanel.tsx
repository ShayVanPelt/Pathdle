"use client";

import type { CompleteResponse } from "@/lib/api/types";
import { SCORING } from "@/lib/api/types";
import { articleTitle } from "@/lib/format";
import { useState } from "react";

type Props = {
  result: CompleteResponse;
  onClose: () => void;
};

function PathRow({
  label,
  colorClass,
  path,
}: {
  label: string;
  colorClass: string;
  path: string[];
}) {
  return (
    <div>
      <p className={`text-[0.7rem] font-semibold uppercase tracking-[0.18em] ${colorClass}`}>
        {label}
      </p>
      <div className="mt-2 flex flex-wrap items-center gap-1.5">
        {path.map((id, i) => (
          <span key={`${id}-${i}`} className="flex items-center gap-1.5">
            {i > 0 && <span className="text-[var(--ink-dim)]">→</span>}
            <span className="rounded-full bg-[oklch(0.16_0.03_275_/_0.7)] px-2.5 py-1 text-[0.95rem] text-[var(--ink-bright)]">
              {articleTitle(id)}
            </span>
          </span>
        ))}
      </div>
    </div>
  );
}

export function ResultsPanel({ result, onClose }: Props) {
  const [copied, setCopied] = useState(false);
  const linkCost = result.playerConnectionCount * SCORING.successfulLink;
  const hintCost = Math.max(0, result.score - linkCost);
  const hintCount = Math.round(hintCost / SCORING.revealOutbound);
  const shareText = `Pathdle · score ${result.score} · ${result.playerConnectionCount} links · ${Math.round(result.efficiency * 100)}% efficiency\n${result.playerPath.map(articleTitle).join(" → ")}`;

  const share = async () => {
    try {
      if (navigator.share) {
        await navigator.share({ title: "Pathdle", text: shareText });
        return;
      }
      await navigator.clipboard.writeText(shareText);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      try {
        await navigator.clipboard.writeText(shareText);
        setCopied(true);
        window.setTimeout(() => setCopied(false), 1800);
      } catch {
        /* ignore */
      }
    }
  };

  return (
    <div className="absolute inset-0 z-30 flex items-center justify-center bg-[oklch(0.04_0.02_275_/_0.72)] p-5 backdrop-blur-sm">
      <div
        role="dialog"
        aria-labelledby="results-title"
        className="pathdle-instrument w-full max-w-xl rounded-3xl p-6 shadow-2xl sm:p-9"
      >
        <p className="text-[0.7rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent)]">
          Target acquired
        </p>
        <h2
          id="results-title"
          className="mt-2 font-[family-name:var(--font-display)] text-4xl leading-none text-[var(--ink-bright)] sm:text-5xl"
        >
          {Math.round(result.efficiency * 100)}% efficient
        </h2>
        <p className="mt-3 text-base text-[var(--ink-muted)]">
          Score {result.score} from {result.playerConnectionCount} links vs optimal{" "}
          {result.optimalLength}.
        </p>

        <dl className="mt-6 grid grid-cols-2 gap-3 text-base">
          <div className="rounded-2xl bg-[oklch(0.14_0.03_72_/_0.45)] px-4 py-3">
            <dt className="text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-[var(--accent)]">
              Link attempts
            </dt>
            <dd className="mt-1 font-[family-name:var(--font-display)] text-2xl text-[var(--ink-bright)]">
              {result.playerConnectionCount} × {SCORING.successfulLink}
            </dd>
          </div>
          <div className="rounded-2xl bg-[oklch(0.14_0.04_210_/_0.4)] px-4 py-3">
            <dt className="text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-[var(--hint)]">
              Reveals
            </dt>
            <dd className="mt-1 font-[family-name:var(--font-display)] text-2xl text-[var(--ink-bright)]">
              {hintCount} × {SCORING.revealOutbound}
            </dd>
          </div>
        </dl>

        <div className="mt-6 space-y-5">
          <PathRow label="Your path" colorClass="text-[var(--accent)]" path={result.playerPath} />
          <PathRow label="Optimal path" colorClass="text-[var(--target)]" path={result.optimalPath} />
        </div>

        <div className="mt-8 flex flex-col gap-3 sm:flex-row">
          <button
            type="button"
            onClick={() => void share()}
            className="flex-1 rounded-xl bg-[var(--accent)] px-4 py-3.5 text-base font-semibold text-[oklch(0.12_0.03_80)] transition hover:brightness-110"
          >
            {copied ? "Copied" : "Share voyage"}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-xl border border-[var(--hud-border)] px-4 py-3.5 text-base text-[var(--ink-bright)] transition hover:bg-[oklch(0.2_0.03_250_/_0.45)]"
          >
            Keep charting
          </button>
        </div>
      </div>
    </div>
  );
}
