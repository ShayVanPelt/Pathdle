import type {
  AttemptResponse,
  CompleteResponse,
  GameState,
  PublicPuzzle,
  RevealResponse,
} from "./types";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5294";

async function apiFetch<T>(
  path: string,
  init?: RequestInit & { playerKey?: string },
): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Accept", "application/json");
  if (init?.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (init?.playerKey) {
    headers.set("X-Player-Key", init.playerKey);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    let detail = response.statusText;
    try {
      const payload = (await response.json()) as { error?: string };
      if (payload.error) detail = payload.error;
    } catch {
      /* ignore */
    }
    throw new Error(detail || `Request failed (${response.status})`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export function getApiBaseUrl() {
  return API_BASE_URL;
}

export function getTodayPuzzle() {
  return apiFetch<PublicPuzzle>("/api/puzzles/today");
}

export function startGame(playerKey: string, puzzleId?: string) {
  return apiFetch<GameState>("/api/games", {
    method: "POST",
    playerKey,
    body: JSON.stringify(puzzleId ? { puzzleId } : {}),
  });
}

export function getGame(gameId: string, playerKey: string) {
  return apiFetch<GameState>(`/api/games/${gameId}`, { playerKey });
}

export function attemptConnection(
  gameId: string,
  playerKey: string,
  fromId: string,
  toId: string,
) {
  return apiFetch<AttemptResponse>(`/api/games/${gameId}/attempts`, {
    method: "POST",
    playerKey,
    body: JSON.stringify({ fromId, toId }),
  });
}

export function revealOutbound(
  gameId: string,
  playerKey: string,
  articleId: string,
) {
  return apiFetch<RevealResponse>(`/api/games/${gameId}/reveals`, {
    method: "POST",
    playerKey,
    body: JSON.stringify({ articleId }),
  });
}

export function completeGame(gameId: string, playerKey: string) {
  return apiFetch<CompleteResponse>(`/api/games/${gameId}/complete`, {
    method: "POST",
    playerKey,
  });
}
