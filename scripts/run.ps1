# PostgreSQL, API (http://localhost:5270/docs), web app (http://localhost:5174), Mailpit (http://localhost:8025).
Set-Location (Join-Path $PSScriptRoot "..")
docker compose -f infra/docker-compose.yml up --build @args
