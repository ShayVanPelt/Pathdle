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

-- Migrations for existing DBs

-- Add hint_edges if an earlier 001 without this column was already applied.
alter table player_games
  add column if not exists hint_edges jsonb not null default '[]'::jsonb;
-- Group-graph revamp: paid hint budget. Edge group labels live in jsonb payloads.
alter table player_games
  add column if not exists hints_used int not null default 0;

comment on column player_games.hints_used is
  'Paid hint reveals used (max 3). Re-showing previously revealed neighbors is free.';


-- Hand-authored group-graph seed (Dell → Apple → Orange → Vitamin_C).
-- Fresh `docker compose up` applies this via docker-entrypoint-initdb.d.
-- API also upserts today's seed on startup when Pathdle:Storage=Postgres (never overwrites).

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
  to_char(current_date, 'YYYY-MM-DD') || '.group-seed',
  'Dell',
  'Vitamin_C',
  '[
    {"id":"Dell","title":"Dell","x":0.10,"y":0.48,"kind":"start"},
    {"id":"Apple","title":"Apple","x":0.38,"y":0.42,"kind":"normal"},
    {"id":"Orange","title":"Orange","x":0.62,"y":0.48,"kind":"normal"},
    {"id":"Vitamin_C","title":"Vitamin C","x":0.90,"y":0.52,"kind":"target"},
    {"id":"Microsoft","title":"Microsoft","x":0.22,"y":0.28,"kind":"normal"},
    {"id":"Google","title":"Google","x":0.28,"y":0.18,"kind":"normal"},
    {"id":"Sony","title":"Sony","x":0.18,"y":0.62,"kind":"normal"},
    {"id":"Banana","title":"Banana","x":0.55,"y":0.28,"kind":"normal"},
    {"id":"Pear","title":"Pear","x":0.70,"y":0.32,"kind":"normal"},
    {"id":"Lemon","title":"Lemon","x":0.72,"y":0.62,"kind":"normal"},
    {"id":"HP","title":"HP","x":0.12,"y":0.32,"kind":"normal"},
    {"id":"Intel","title":"Intel","x":0.32,"y":0.55,"kind":"normal"},
    {"id":"iPhone","title":"iPhone","x":0.42,"y":0.22,"kind":"normal"},
    {"id":"Macintosh","title":"Macintosh","x":0.48,"y":0.58,"kind":"normal"},
    {"id":"California","title":"California","x":0.35,"y":0.72,"kind":"normal"},
    {"id":"Cupertino","title":"Cupertino","x":0.45,"y":0.78,"kind":"normal"},
    {"id":"Citrus","title":"Citrus","x":0.68,"y":0.72,"kind":"normal"},
    {"id":"Ascorbic_acid","title":"Ascorbic acid","x":0.82,"y":0.38,"kind":"normal"},
    {"id":"Scurvy","title":"Scurvy","x":0.88,"y":0.68,"kind":"normal"},
    {"id":"Kiwi_fruit","title":"Kiwi fruit","x":0.58,"y":0.18,"kind":"normal"},
    {"id":"Strawberry","title":"Strawberry","x":0.78,"y":0.22,"kind":"normal"},
    {"id":"Samsung","title":"Samsung","x":0.20,"y":0.78,"kind":"normal"},
    {"id":"PlayStation","title":"PlayStation","x":0.08,"y":0.72,"kind":"normal"},
    {"id":"Windows","title":"Windows","x":0.30,"y":0.38,"kind":"normal"},
    {"id":"Android","title":"Android","x":0.38,"y":0.12,"kind":"normal"},
    {"id":"Steve_Jobs","title":"Steve Jobs","x":0.50,"y":0.68,"kind":"normal"},
    {"id":"Tim_Cook","title":"Tim Cook","x":0.52,"y":0.85,"kind":"normal"},
    {"id":"Fruit_salad","title":"Fruit salad","x":0.65,"y":0.55,"kind":"normal"},
    {"id":"Orange_juice","title":"Orange juice","x":0.75,"y":0.48,"kind":"normal"},
    {"id":"Broccoli","title":"Broccoli","x":0.85,"y":0.28,"kind":"normal"},
    {"id":"Pepper","title":"Bell pepper","x":0.82,"y":0.58,"kind":"normal"},
    {"id":"Nintendo","title":"Nintendo","x":0.15,"y":0.88,"kind":"normal"}
  ]'::jsonb,
  '[
    {"from":"Dell","to":"Apple","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Apple","to":"Orange","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Orange","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Dell","to":"Microsoft","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Dell","to":"HP","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Apple","to":"Microsoft","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Apple","to":"Google","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Apple","to":"Sony","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Microsoft","to":"Google","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Microsoft","to":"Sony","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Google","to":"Samsung","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Sony","to":"Samsung","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"HP","to":"Intel","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Intel","to":"Microsoft","groupId":"industry:tech_company","groupLabel":"Technology company"},
    {"from":"Apple","to":"Banana","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Apple","to":"Pear","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Orange","to":"Banana","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Orange","to":"Pear","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Orange","to":"Lemon","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Banana","to":"Pear","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Banana","to":"Kiwi_fruit","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Pear","to":"Kiwi_fruit","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Lemon","to":"Kiwi_fruit","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Kiwi_fruit","to":"Strawberry","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Strawberry","to":"Orange","groupId":"category:fruit","groupLabel":"Fruit"},
    {"from":"Lemon","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Kiwi_fruit","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Strawberry","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Broccoli","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Pepper","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Orange_juice","to":"Vitamin_C","groupId":"nutrient:vitamin_c","groupLabel":"Contains vitamin C"},
    {"from":"Orange","to":"Lemon","groupId":"botany:citrus","groupLabel":"Citrus fruit"},
    {"from":"Orange","to":"Citrus","groupId":"botany:citrus","groupLabel":"Citrus fruit"},
    {"from":"Lemon","to":"Citrus","groupId":"botany:citrus","groupLabel":"Citrus fruit"},
    {"from":"Apple","to":"iPhone","groupId":"maker:apple_inc","groupLabel":"Made by Apple"},
    {"from":"Apple","to":"Macintosh","groupId":"maker:apple_inc","groupLabel":"Made by Apple"},
    {"from":"iPhone","to":"Macintosh","groupId":"maker:apple_inc","groupLabel":"Made by Apple"},
    {"from":"Apple","to":"Steve_Jobs","groupId":"org:apple_leadership","groupLabel":"Apple leadership"},
    {"from":"Apple","to":"Tim_Cook","groupId":"org:apple_leadership","groupLabel":"Apple leadership"},
    {"from":"Steve_Jobs","to":"Tim_Cook","groupId":"org:apple_leadership","groupLabel":"Apple leadership"},
    {"from":"Apple","to":"Cupertino","groupId":"hq:cupertino","groupLabel":"Based in Cupertino"},
    {"from":"Cupertino","to":"California","groupId":"place:california","groupLabel":"Located in California"},
    {"from":"Google","to":"California","groupId":"place:california","groupLabel":"Located in California"},
    {"from":"Microsoft","to":"Windows","groupId":"platform:os_vendor","groupLabel":"Operating system vendor"},
    {"from":"Google","to":"Android","groupId":"platform:os_vendor","groupLabel":"Operating system vendor"},
    {"from":"Sony","to":"PlayStation","groupId":"franchise:sony_gaming","groupLabel":"Sony gaming"},
    {"from":"Nintendo","to":"PlayStation","groupId":"category:game_console","groupLabel":"Game console maker"},
    {"from":"Sony","to":"Nintendo","groupId":"category:game_console","groupLabel":"Game console maker"},
    {"from":"Samsung","to":"Android","groupId":"platform:android_devices","groupLabel":"Android devices"},
    {"from":"Banana","to":"Fruit_salad","groupId":"dish:fruit_salad","groupLabel":"Fruit salad ingredient"},
    {"from":"Orange","to":"Fruit_salad","groupId":"dish:fruit_salad","groupLabel":"Fruit salad ingredient"},
    {"from":"Strawberry","to":"Fruit_salad","groupId":"dish:fruit_salad","groupLabel":"Fruit salad ingredient"},
    {"from":"Orange","to":"Orange_juice","groupId":"product:orange_juice","groupLabel":"Orange juice"},
    {"from":"Ascorbic_acid","to":"Vitamin_C","groupId":"chem:ascorbic","groupLabel":"Also known as"},
    {"from":"Scurvy","to":"Vitamin_C","groupId":"medicine:scurvy","groupLabel":"Prevents scurvy"},
    {"from":"Broccoli","to":"Pepper","groupId":"category:vegetable","groupLabel":"Vegetable"}
  ]'::jsonb,
  '["Dell","Apple","Orange","Vitamin_C"]'::jsonb,
  3,
  '{"note":"hand-authored group-graph seed","band":"medium"}'::jsonb,
  'seed-group-graph-v1',
  'hand-authored-groups-v1'
)
on conflict (puzzle_date) do nothing;
