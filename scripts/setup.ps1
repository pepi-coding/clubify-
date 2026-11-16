$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
. ./scripts/dev-env.ps1 -Seed
function Check { if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" } }
docker compose -f infra/compose.yaml up -d --wait
Check
dotnet restore --locked-mode
Check
dotnet tool restore
Check
dotnet run --project src/backend/ClubPlatform.Api -- --migrate
Check
Push-Location src/web
try { npm ci; Check; npm run build; Check } finally { Pop-Location }
Write-Host 'Setup complete. Start the API and web in separate terminals using README.md.'
