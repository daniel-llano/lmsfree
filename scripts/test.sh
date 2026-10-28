#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test LMSFree.slnx
(cd web && { [ -d node_modules ] || npm ci --no-audit --no-fund; } && npm run typecheck && npm test && npm run build)
if [ "${E2E:-}" = "1" ]; then
  SKIP_INSTALL=1 ./scripts/e2e.sh
fi
