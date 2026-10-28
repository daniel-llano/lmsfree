#!/usr/bin/env bash
# PostgreSQL, API (http://localhost:5270/docs), web app (http://localhost:5174), Mailpit (http://localhost:8025).
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose -f infra/docker-compose.yml up --build "$@"
