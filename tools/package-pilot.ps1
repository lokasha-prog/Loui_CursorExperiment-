# Builds a ready-to-install pilot package in dist\pilot: the API, the built web app inside it, and the install scripts.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\pilot'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

Push-Location (Join-Path $root 'web'); pnpm install --frozen-lockfile; pnpm build; Pop-Location
dotnet publish (Join-Path $root 'backend\src\NMS.Api') -c Release -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'API publish failed' }
Copy-Item (Join-Path $root 'web\dist') (Join-Path $out 'wwwroot') -Recurse
Copy-Item (Join-Path $PSScriptRoot 'install-service.ps1'), (Join-Path $PSScriptRoot 'backup-db.ps1') $out
Copy-Item (Join-Path $root 'docs\runbooks\PILOT-RUNBOOK.md') $out
Write-Host "Package ready: $out" -ForegroundColor Green
