export type NodeKind = "start" | "target" | "normal";

export type PuzzleNode = {
  id: string;
  title: string;
  x: number;
  y: number;
  kind: NodeKind;
  /** Short Wikidata English description, when available. */
  description?: string | null;
};

export type Edge = {
  from: string;
  to: string;
  /** Present on discovered edges only — never on hints. */
  groupId?: string | null;
  groupLabel?: string | null;
};

export type PublicPuzzle = {
  id: string;
  puzzleDate: string;
  graphVersion: string;
  startArticleId: string;
  targetArticleId: string;
  nodes: PuzzleNode[];
};

export type GameState = {
  gameId: string;
  puzzleId: string;
  status: string;
  startArticleId: string;
  targetArticleId: string;
  nodes: PuzzleNode[];
  discoveredEdges: Edge[];
  /** Visual-only reveal hints; not part of the path until confirmed by drag. */
  hintEdges: Edge[];
  playerPath: string[];
  revealedArticleIds: string[];
  connectionCount: number;
  score: number;
  hintsUsed: number;
  hintsRemaining: number;
  reachedTarget: boolean;
};

export type AttemptResponse = {
  success: boolean;
  fromId: string;
  toId: string;
  groupId?: string | null;
  groupLabel?: string | null;
  pointsAdded: number;
  connectionCount: number;
  score: number;
  hintsUsed: number;
  hintsRemaining: number;
  discoveredEdges: Edge[];
  hintEdges: Edge[];
  playerPath: string[];
  revealedArticleIds: string[];
  reachedTarget: boolean;
  message: string;
};

export type RevealResponse = {
  articleId: string;
  neighborIds: string[];
  hintEdgesAdded: Edge[];
  pointsAdded: number;
  connectionCount: number;
  score: number;
  hintsUsed: number;
  hintsRemaining: number;
  discoveredEdges: Edge[];
  hintEdges: Edge[];
  playerPath: string[];
  revealedArticleIds: string[];
  reachedTarget: boolean;
  message: string;
};

export type CompleteResponse = {
  gameId: string;
  status: string;
  playerPath: string[];
  playerConnectionCount: number;
  optimalPath: string[];
  optimalLength: number;
  efficiency: number;
  score: number;
};

/** Keep in sync with Pathdle.Application.ScoringRules */
export const SCORING = {
  successfulLink: 100,
  failedLink: 200,
  revealOutbound: 75,
  maxHintsPerGame: 3,
} as const;
