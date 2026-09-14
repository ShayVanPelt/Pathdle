const STORAGE_KEY = "pathdle.player_key";

/** Anonymous browser identity for MVP. Opt-in auth links this later. */
export function getOrCreatePlayerKey(): string {
  if (typeof window === "undefined") {
    throw new Error("getOrCreatePlayerKey must run in the browser");
  }

  const existing = window.localStorage.getItem(STORAGE_KEY);
  if (existing) {
    return existing;
  }

  const playerKey = crypto.randomUUID();
  window.localStorage.setItem(STORAGE_KEY, playerKey);
  return playerKey;
}
