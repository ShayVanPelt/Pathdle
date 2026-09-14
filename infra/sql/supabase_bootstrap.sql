-- Pathdle: one-shot bootstrap for Supabase SQL Editor
-- Safe to re-run: seed uses ON CONFLICT DO NOTHING; hint_edges uses IF NOT EXISTS.

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
  -- [{ "from": "Albert_Einstein", "to": "Physics" }, ...]
  edges jsonb not null,
  -- SERVER ONLY until game complete.
  -- ["Albert_Einstein", "Physics", "Mathematics", "Nintendo"]
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
  -- Article ids whose outbound neighbors have been revealed.
  reveals jsonb not null default '[]'::jsonb,
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


-- Add hint_edges if an earlier 001 without this column was already applied.
alter table player_games
  add column if not exists hint_edges jsonb not null default '[]'::jsonb;


-- Optional: seed the frozen MVP puzzle into Postgres.
-- Fresh `docker compose up` applies this automatically via docker-entrypoint-initdb.d.
-- The API also upserts today's seed on startup when Pathdle:Storage=Postgres (never overwrites).

insert into daily_puzzles (
  id,
  puzzle_date,
  graph_version,
  start_article_id,
  target_article_id,
  nodes,
  edges,
  optimal_path,
  optimal_length,
  difficulty,
  generator_seed,
  corpus_version
) values (
  '11111111-1111-1111-1111-111111111111',
  current_date,
  to_char(current_date, 'YYYY-MM-DD') || '.seed',
  'Albert_Einstein',
  'Nintendo',
  '[
    {"id":"Albert_Einstein","title":"Albert Einstein","x":0.12,"y":0.48,"kind":"start"},
    {"id":"Physics","title":"Physics","x":0.32,"y":0.28,"kind":"normal"},
    {"id":"Mathematics","title":"Mathematics","x":0.55,"y":0.35,"kind":"normal"},
    {"id":"Nintendo","title":"Nintendo","x":0.88,"y":0.52,"kind":"target"},
    {"id":"Germany","title":"Germany","x":0.22,"y":0.72,"kind":"normal"},
    {"id":"Japan","title":"Japan","x":0.62,"y":0.78,"kind":"normal"},
    {"id":"Relativity","title":"Relativity","x":0.28,"y":0.12,"kind":"normal"},
    {"id":"Quantum_mechanics","title":"Quantum mechanics","x":0.45,"y":0.15,"kind":"normal"},
    {"id":"Geometry","title":"Geometry","x":0.58,"y":0.18,"kind":"normal"},
    {"id":"Science","title":"Science","x":0.40,"y":0.48,"kind":"normal"},
    {"id":"Computer_science","title":"Computer science","x":0.68,"y":0.42,"kind":"normal"},
    {"id":"Video_game","title":"Video game","x":0.78,"y":0.30,"kind":"normal"},
    {"id":"Berlin","title":"Berlin","x":0.10,"y":0.85,"kind":"normal"},
    {"id":"Kyoto","title":"Kyoto","x":0.72,"y":0.90,"kind":"normal"},
    {"id":"Shigeru_Miyamoto","title":"Shigeru Miyamoto","x":0.90,"y":0.70,"kind":"normal"},
    {"id":"Philosophy","title":"Philosophy","x":0.48,"y":0.62,"kind":"normal"}
  ]'::jsonb,
  '[
    {"from":"Albert_Einstein","to":"Physics"},
    {"from":"Albert_Einstein","to":"Relativity"},
    {"from":"Albert_Einstein","to":"Germany"},
    {"from":"Albert_Einstein","to":"Science"},
    {"from":"Physics","to":"Mathematics"},
    {"from":"Physics","to":"Quantum_mechanics"},
    {"from":"Physics","to":"Relativity"},
    {"from":"Physics","to":"Science"},
    {"from":"Mathematics","to":"Geometry"},
    {"from":"Mathematics","to":"Computer_science"},
    {"from":"Mathematics","to":"Nintendo"},
    {"from":"Germany","to":"Berlin"},
    {"from":"Germany","to":"Japan"},
    {"from":"Japan","to":"Kyoto"},
    {"from":"Japan","to":"Nintendo"},
    {"from":"Relativity","to":"Physics"},
    {"from":"Quantum_mechanics","to":"Physics"},
    {"from":"Science","to":"Physics"},
    {"from":"Science","to":"Mathematics"},
    {"from":"Science","to":"Philosophy"},
    {"from":"Computer_science","to":"Mathematics"},
    {"from":"Computer_science","to":"Video_game"},
    {"from":"Video_game","to":"Nintendo"},
    {"from":"Video_game","to":"Shigeru_Miyamoto"},
    {"from":"Shigeru_Miyamoto","to":"Nintendo"},
    {"from":"Geometry","to":"Mathematics"},
    {"from":"Berlin","to":"Germany"},
    {"from":"Kyoto","to":"Japan"},
    {"from":"Philosophy","to":"Science"},
    {"from":"Nintendo","to":"Video_game"}
  ]'::jsonb,
  '["Albert_Einstein","Physics","Mathematics","Nintendo"]'::jsonb,
  3,
  '{"note":"hand-authored seed"}'::jsonb,
  'seed-mvp-v1',
  'hand-authored-mvp'
)
on conflict (puzzle_date) do nothing;

