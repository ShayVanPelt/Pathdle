-- Add hint_edges if an earlier 001 without this column was already applied.
alter table player_games
  add column if not exists hint_edges jsonb not null default '[]'::jsonb;
