#!/usr/bin/env bash
# Apply migrations + seed to an already-running Postgres (e.g. after volume wipe skip).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONN="${DATABASE_URL:-postgresql://pathdle:pathdle@localhost:5432/pathdle}"

psql "$CONN" -v ON_ERROR_STOP=1 -f "$ROOT/infra/sql/migrations/001_init.sql"
psql "$CONN" -v ON_ERROR_STOP=1 -f "$ROOT/infra/sql/migrations/002_hint_edges.sql"
psql "$CONN" -v ON_ERROR_STOP=1 -f "$ROOT/infra/sql/migrations/003_group_graph.sql"
psql "$CONN" -v ON_ERROR_STOP=1 -f "$ROOT/infra/sql/seed/001_seed_puzzle.sql"
echo "Pathdle schema + seed applied."
