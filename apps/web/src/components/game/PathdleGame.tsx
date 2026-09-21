"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  attemptConnection,
  completeGame,
  getTodayPuzzle,
  revealOutbound,
  startGame,
} from "@/lib/api/client";
import type { CompleteResponse, GameState } from "@/lib/api/types";
import { chartedNodeIds } from "@/lib/game/charted";
import { getOrCreatePlayerKey } from "@/lib/playerKey";
import { FieldAtmosphere } from "./FieldAtmosphere";
import { GameBoard } from "./GameBoard";
import { GameHud } from "./GameHud";
import { HowToPlay } from "./HowToPlay";
import { ResultsPanel } from "./ResultsPanel";

const HELP_SEEN_KEY = "pathdle.howto_seen.v5";

export function PathdleGame() {
  const [game, setGame] = useState<GameState | null>(null);
  const [playerKey, setPlayerKey] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [completing, setCompleting] = useState(false);
  const [result, setResult] = useState<CompleteResponse | null>(null);
  const [helpOpen, setHelpOpen] = useState(false);
  const [menuNodeId, setMenuNodeId] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [highlightedNeighborIds, setHighlightedNeighborIds] = useState<string[]>(
    [],
  );
  const autoCompleteRef = useRef(false);

  useEffect(() => {
    let cancelled = false;

    async function boot() {
      try {
        const key = getOrCreatePlayerKey();
        if (cancelled) return;
        setPlayerKey(key);

        const seen = window.localStorage.getItem(HELP_SEEN_KEY);
        if (!seen) {
          setHelpOpen(true);
        }

        await getTodayPuzzle();
        const state = await startGame(key);
        if (cancelled) return;
        setGame({
          ...state,
          revealedArticleIds: state.revealedArticleIds ?? [],
          hintEdges: state.hintEdges ?? [],
          hintsUsed: state.hintsUsed ?? 0,
          hintsRemaining: state.hintsRemaining ?? 3,
        });
        setSelectedId(state.startArticleId);
      } catch (err) {
        if (cancelled) return;
        setError(
          err instanceof Error
            ? err.message
            : "Could not reach the Pathdle API. Is it running on :5294?",
        );
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void boot();
    return () => {
      cancelled = true;
    };
  }, []);

  const closeHelp = useCallback(() => {
    window.localStorage.setItem(HELP_SEEN_KEY, "1");
    setHelpOpen(false);
  }, []);

  const showFeedback = useCallback((message: string) => {
    setFeedback(message);
    window.setTimeout(() => setFeedback(null), 2400);
  }, []);

  const onAttempt = useCallback(
    async (fromId: string, toId: string) => {
      if (!game || !playerKey || game.status !== "active") return false;
      if (!chartedNodeIds(game.startArticleId, game.discoveredEdges).has(fromId)) {
        showFeedback("Chart a path to this star before exploring from it.");
        return false;
      }

      try {
        const response = await attemptConnection(
          game.gameId,
          playerKey,
          fromId,
          toId,
        );
        setGame((prev) =>
          prev
            ? {
                ...prev,
                discoveredEdges: response.discoveredEdges,
                hintEdges: response.hintEdges ?? [],
                playerPath: response.playerPath,
                revealedArticleIds: response.revealedArticleIds ?? [],
                connectionCount: response.connectionCount,
                score: response.score,
                hintsUsed: response.hintsUsed ?? prev.hintsUsed,
                hintsRemaining: response.hintsRemaining ?? prev.hintsRemaining,
                reachedTarget: response.reachedTarget,
              }
            : prev,
        );
        showFeedback(response.message);
        return response.success;
      } catch (err) {
        showFeedback(err instanceof Error ? err.message : "That attempt failed");
        return false;
      }
    },
    [game, playerKey, showFeedback],
  );

  const onRevealRequest = useCallback(
    async (articleId: string) => {
      if (!game || !playerKey || game.status !== "active") return;
      if (!chartedNodeIds(game.startArticleId, game.discoveredEdges).has(articleId)) {
        showFeedback("Chart a path to this star before exploring from it.");
        return;
      }

      try {
        const response = await revealOutbound(game.gameId, playerKey, articleId);
        setGame((prev) =>
          prev
            ? {
                ...prev,
                discoveredEdges: response.discoveredEdges,
                hintEdges: response.hintEdges ?? [],
                playerPath: response.playerPath,
                revealedArticleIds: response.revealedArticleIds ?? [],
                connectionCount: response.connectionCount,
                score: response.score,
                hintsUsed: response.hintsUsed ?? prev.hintsUsed,
                hintsRemaining: response.hintsRemaining ?? prev.hintsRemaining,
                reachedTarget: response.reachedTarget,
              }
            : prev,
        );
        setHighlightedNeighborIds(response.neighborIds);
        setMenuNodeId(null);
        showFeedback(response.message);
        window.setTimeout(() => setHighlightedNeighborIds([]), 3500);
      } catch (err) {
        showFeedback(err instanceof Error ? err.message : "Reveal failed");
      }
    },
    [game, playerKey, showFeedback],
  );

  const onComplete = useCallback(async () => {
    if (!game || !playerKey) return;
    setCompleting(true);
    try {
      const done = await completeGame(game.gameId, playerKey);
      setResult(done);
      setGame((prev) => (prev ? { ...prev, status: "completed" } : prev));
    } catch (err) {
      autoCompleteRef.current = false;
      showFeedback(err instanceof Error ? err.message : "Could not complete");
    } finally {
      setCompleting(false);
    }
  }, [game, playerKey, showFeedback]);

  useEffect(() => {
    if (!game?.reachedTarget || game.status !== "active" || autoCompleteRef.current) {
      return;
    }
    autoCompleteRef.current = true;
    void onComplete();
  }, [game?.reachedTarget, game?.status, onComplete]);

  const startNode = useMemo(
    () => game?.nodes.find((n) => n.kind === "start"),
    [game],
  );
  const targetNode = useMemo(
    () => game?.nodes.find((n) => n.kind === "target"),
    [game],
  );

  if (loading) {
    return (
      <div className="relative flex h-dvh items-center justify-center overflow-hidden bg-[var(--field-void)]">
        <FieldAtmosphere />
        <p className="relative z-[1] font-[family-name:var(--font-display)] text-3xl text-[var(--ink-muted)] sm:text-4xl">
          Charting today&apos;s sky…
        </p>
      </div>
    );
  }

  if (error || !game || !startNode || !targetNode) {
    return (
      <div className="relative flex h-dvh flex-col items-center justify-center gap-3 overflow-hidden bg-[var(--field-void)] px-6 text-center">
        <FieldAtmosphere />
        <p className="relative z-[1] font-[family-name:var(--font-display)] text-4xl text-[var(--ink-bright)]">
          Pathdle
        </p>
        <p className="relative z-[1] max-w-md text-lg text-[var(--ink-muted)]">
          {error ?? "No game loaded."}
        </p>
        <p className="relative z-[1] text-base text-[var(--ink-muted)]">
          Run{" "}
          <code className="text-[var(--accent)]">
            dotnet run --project apps/api/Pathdle.Api --launch-profile http
          </code>
        </p>
      </div>
    );
  }

  return (
    <div className="relative h-dvh w-full overflow-hidden bg-[var(--field-void)]">
      <GameBoard
        nodes={game.nodes}
        discoveredEdges={game.discoveredEdges}
        hintEdges={game.hintEdges ?? []}
        playerPath={game.playerPath}
        revealedArticleIds={game.revealedArticleIds ?? []}
        highlightedNeighborIds={highlightedNeighborIds}
        selectedId={selectedId}
        menuNodeId={menuNodeId}
        playLocked={helpOpen || Boolean(result)}
        hintsRemaining={game.hintsRemaining ?? 3}
        onSelectedChange={setSelectedId}
        onAttempt={onAttempt}
        onNodeClick={(id) => {
          setSelectedId(id);
          setMenuNodeId(id);
        }}
        onRevealRequest={onRevealRequest}
        onCloseMenu={() => setMenuNodeId(null)}
        feedback={feedback}
      />
      <GameHud
        brand="Pathdle"
        connectionCount={game.connectionCount}
        score={game.score}
        hintsRemaining={game.hintsRemaining ?? 3}
        path={game.playerPath}
        startId={startNode.id}
        targetId={targetNode.id}
        startTitle={startNode.title}
        targetTitle={targetNode.title}
        titles={Object.fromEntries(game.nodes.map((n) => [n.id, n.title]))}
        selectedId={selectedId}
        reachedTarget={game.reachedTarget}
        onSelectNode={(id) => {
          setSelectedId(id);
          setMenuNodeId(id);
        }}
        onOpenHelp={() => setHelpOpen(true)}
      />
      <HowToPlay open={helpOpen} onClose={closeHelp} />
      {result && (
        <ResultsPanel
          result={result}
          nodes={game.nodes}
          onClose={() => setResult(null)}
        />
      )}
      {completing && !result && (
        <p className="pointer-events-none absolute bottom-28 left-1/2 z-30 -translate-x-1/2 pathdle-hud-chip rounded-full px-5 py-2.5 text-base text-[var(--ink-bright)]">
          Logging the voyage…
        </p>
      )}
    </div>
  );
}
