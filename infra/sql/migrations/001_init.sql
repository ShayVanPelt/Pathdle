-- Pathdle MVP schema
-- Published puzzles are immutable. Gameplay reads Postgres only (not Neo4j).

create extension if not exists "pgcrypto";

create table daily_puzzles (
  id uuid primary key default gen_random_uuid(),
  puzzle_date date not null unique,
  graph_version text not null,
  start_article_id text not null,
  target_article_id text not null,
  -- Public board layout. Example node:
  -- { "id": "Albert_Einstein", "title": "Albert Einstein", "x": 0.12, "y": 0.34, "kind": "start" }
  nodes jsonb not null,
  -- SERVER ONLY. Never return the full set to clients.
  -- [{ "from": "Dell", "to": "Apple", "groupId": "industry:tech", "groupLabel": "Technology company" }, ...]
  edges jsonb not null,
  -- SERVER ONLY until game complete.
  -- ["Dell", "Apple", "Orange", "Vitamin_C"]
  optimal_path jsonb not null,
  optimal_length int not null,
  difficulty jsonb null,
  generator_seed text not null,
  corpus_version text not null,
  published_at timestamptz not null default now(),
  created_at timestamptz not null default now(),
  constraint daily_puzzles_optimal_length_positive check (optimal_length > 0)
);

create table player_games (
  id uuid primary key default gen_random_uuid(),
  daily_puzzle_id uuid not null references daily_puzzles (id),
  -- Anonymous browser UUID from localStorage (MVP).
  player_key text not null,
  -- Filled later when the player opts in to sign-in.
  user_id uuid null,
  status text not null default 'active'
    check (status in ('active', 'completed', 'abandoned')),
  discovered_edges jsonb not null default '[]'::jsonb,
  -- Visual-only outbound hint edges from reveals (not path until confirmed).
  hint_edges jsonb not null default '[]'::jsonb,
  attempted_edges jsonb not null default '[]'::jsonb,
  player_path jsonb not null default '[]'::jsonb,
  -- Points spent / connection count for MVP leaderboard sorting.
  score int not null default 0,
  connection_count int not null default 0,
  -- Article ids whose neighbors have been revealed.
  reveals jsonb not null default '[]'::jsonb,
  -- Paid hint reveals used (max 3). Re-show is free.
  hints_used int not null default 0,
  started_at timestamptz not null default now(),
  completed_at timestamptz null,
  unique (daily_puzzle_id, player_key)
);

create index player_games_player_key_idx on player_games (player_key);
create index player_games_user_id_idx on player_games (user_id) where user_id is not null;
create index player_games_daily_puzzle_id_idx on player_games (daily_puzzle_id);

-- Optional public leaderboard (signed-in users later). Safe to leave empty in MVP.
create table leaderboard_entries (
  daily_puzzle_id uuid not null references daily_puzzles (id),
  player_key text not null,
  user_id uuid null,
  display_name text null,
  score int not null,
  connection_count int not null,
  completed_at timestamptz not null default now(),
  primary key (daily_puzzle_id, player_key)
);

create index leaderboard_entries_daily_score_idx
  on leaderboard_entries (daily_puzzle_id, connection_count, score, completed_at);
