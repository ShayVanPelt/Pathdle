"use client";

import type { CompleteResponse, PuzzleNode } from "@/lib/api/types";
import { SCORING } from "@/lib/api/types";
import { articleTitle } from "@/lib/format";
import { useMemo, useState } from "react";

type Props = {
  result: CompleteResponse;
  nodes: PuzzleNode[];
  onClose: () => void;
};

function PathRow({
  label,
  colorClass,
  path,
  titles,
}: {
  label: string;
  colorClass: string;
  path: string[];
  titles: Record<string, string>;
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
              {articleTitle(id, titles)}
            </span>
          </span>
        ))}
      </div>
    </div>
  );
}

function buildShareText(
  result: CompleteResponse,
  missCount: number,
  hintCount: number,
): string {
  const efficiency = Math.round(result.efficiency * 100);
  const date = new Date().toISOString().slice(0, 10);
  const optimal = result.optimalLength;
  const links = result.playerConnectionCount;
  const extra = Math.max(0, links - optimal);

  const linkRow =
    "🟨".repeat(Math.min(links, optimal)) + (extra > 0 ? "🟧".repeat(extra) : "");
  const missRow = missCount > 0 ? `\n${"🟥".repeat(missCount)}` : "";
  const revealRow = hintCount > 0 ? `\n${"🟦".repeat(hintCount)}` : "";

  const site =
    typeof window !== "undefined" ? window.location.origin.replace(/\/$/, "") : "pathdle";

  return [
    `Pathdle ${date}`,
    `${result.score} pts | ${links}/${optimal} | ${efficiency}%`,
    ``,
    `${linkRow}${missRow}${revealRow}`,
    ``,
    site,
  ].join("\n");
}

async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    try {
      const area = document.createElement("textarea");
      area.value = text;
      area.setAttribute("readonly", "");
      area.style.position = "fixed";
      area.style.left = "-9999px";
      document.body.appendChild(area);
      area.select();
      const ok = document.execCommand("copy");
      document.body.removeChild(area);
      return ok;
    } catch {
      return false;
    }
  }
}

export function ResultsPanel({ result, nodes, onClose }: Props) {
  const [copied, setCopied] = useState(false);
  const titles = useMemo(
    () => Object.fromEntries(nodes.map((n) => [n.id, n.title])),
    [nodes],
  );
  const optimalScore = result.optimalLength * SCORING.successfulLink;
  const foundOptimal = result.score === optimalScore;
  const linkCost = result.playerConnectionCount * SCORING.successfulLink;
  const remainder = Math.max(0, result.score - linkCost);
  const missCount = Math.floor(remainder / SCORING.failedLink);
  const afterMisses = remainder - missCount * SCORING.failedLink;
  const hintCount = Math.round(afterMisses / SCORING.revealOutbound);
  const aboveOptimal = Math.max(0, result.score - optimalScore);

  const share = async () => {
    const shareText = buildShareText(result, missCount, hintCount);
    const ok = await copyText(shareText);
    if (!ok) return;
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1800);
  };

  return (
    <div className="absolute inset-0 z-30 flex items-center justify-center bg-[oklch(0.04_0.02_275_/_0.72)] p-5 backdrop-blur-sm">
      <div
        role="dialog"
        aria-labelledby="results-title"
        className="pathdle-instrument w-full max-w-xl rounded-3xl p-6 shadow-2xl sm:p-9"
      >
        <p className="text-[0.7rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent)]">
          {foundOptimal ? "Optimal path found" : "Target acquired"}
        </p>
        <h2
          id="results-title"
          className="mt-2 font-[family-name:var(--font-display)] text-4xl leading-none text-[var(--ink-bright)] sm:text-5xl"
        >
          {result.score}
        </h2>
        <p className="mt-3 text-base text-[var(--ink-muted)]">
          {foundOptimal
            ? `Perfect score — ${result.optimalLength} connections, no misses or hints.`
            : `${aboveOptimal} above optimal (${optimalScore}). ${result.playerConnectionCount} links vs ${result.optimalLength} optimal.`}
        </p>

        <dl className="mt-6 grid grid-cols-3 gap-2 text-base sm:gap-3">
          <div className="rounded-2xl bg-[oklch(0.14_0.03_72_/_0.45)] px-3 py-3 sm:px-4">
            <dt className="text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-[var(--accent)]">
              Links
            </dt>
            <dd className="mt-1 font-[family-name:var(--font-display)] text-xl text-[var(--ink-bright)] sm:text-2xl">
              {result.playerConnectionCount}×{SCORING.successfulLink}
            </dd>
          </div>
          <div className="rounded-2xl bg-[oklch(0.14_0.05_25_/_0.4)] px-3 py-3 sm:px-4">
            <dt className="text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-[var(--fail)]">
              Misses
            </dt>
            <dd className="mt-1 font-[family-name:var(--font-display)] text-xl text-[var(--ink-bright)] sm:text-2xl">
              {missCount}×{SCORING.failedLink}
            </dd>
          </div>
          <div className="rounded-2xl bg-[oklch(0.14_0.04_210_/_0.4)] px-3 py-3 sm:px-4">
            <dt className="text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-[var(--hint)]">
              Hints
            </dt>
            <dd className="mt-1 font-[family-name:var(--font-display)] text-xl text-[var(--ink-bright)] sm:text-2xl">
              {hintCount}×{SCORING.revealOutbound}
            </dd>
          </div>
        </dl>

        <div className="mt-6 space-y-5">
          <PathRow
            label="Your path"
            colorClass="text-[var(--accent)]"
            path={result.playerPath}
            titles={titles}
          />
          <PathRow
            label="Optimal path"
            colorClass="text-[var(--target)]"
            path={result.optimalPath}
            titles={titles}
          />
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
