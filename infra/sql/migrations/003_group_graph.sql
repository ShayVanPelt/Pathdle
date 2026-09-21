-- Group-graph revamp: paid hint budget. Edge group labels live in jsonb payloads.
alter table player_games
  add column if not exists hints_used int not null default 0;

comment on column player_games.hints_used is
  'Paid hint reveals used (max 3). Re-showing previously revealed neighbors is free.';
