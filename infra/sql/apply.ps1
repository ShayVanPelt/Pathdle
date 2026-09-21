# Apply migrations + seed when Docker init already ran, or against an external DB.
param(
  [string]$ConnectionString = "Host=localhost;Port=5432;Database=pathdle;Username=pathdle;Password=pathdle"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

function Invoke-SqlFile([string]$file) {
  Write-Host "Applying $file ..."
  docker exec -i pathdle-postgres psql -U pathdle -d pathdle -v ON_ERROR_STOP=1 < $file
}

# Prefer docker exec (no local psql required)
if (docker ps --format "{{.Names}}" | Select-String -Quiet "^pathdle-postgres$") {
  Get-Content (Join-Path $root "infra\sql\migrations\001_init.sql") -Raw | docker exec -i pathdle-postgres psql -U pathdle -d pathdle -v ON_ERROR_STOP=1
  Get-Content (Join-Path $root "infra\sql\migrations\002_hint_edges.sql") -Raw | docker exec -i pathdle-postgres psql -U pathdle -d pathdle -v ON_ERROR_STOP=1
  Get-Content (Join-Path $root "infra\sql\migrations\003_group_graph.sql") -Raw | docker exec -i pathdle-postgres psql -U pathdle -d pathdle -v ON_ERROR_STOP=1
  Get-Content (Join-Path $root "infra\sql\seed\001_seed_puzzle.sql") -Raw | docker exec -i pathdle-postgres psql -U pathdle -d pathdle -v ON_ERROR_STOP=1
  Write-Host "Pathdle schema + seed applied via docker exec."
  return
}

Write-Error "Container pathdle-postgres is not running. Start with: docker compose up -d"
