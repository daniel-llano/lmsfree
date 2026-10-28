param([switch]$E2E)
Set-Location (Join-Path $PSScriptRoot "..")
dotnet test LMSFree.slnx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Push-Location web
try {
  if (-not (Test-Path node_modules)) { npm ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
  npm run typecheck; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  npm test; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  npm run build; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }
if ($E2E -or $env:E2E -eq "1") {
  & (Join-Path $PSScriptRoot "e2e.ps1") -SkipInstall
  exit $LASTEXITCODE
}
