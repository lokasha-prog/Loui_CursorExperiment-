<#
.SYNOPSIS
  Saves the AI Brain's Groq API key in this computer's local secrets and restarts the local API so it is used.
  The key is typed in by you, is never shown on screen and is never written into the project files.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root 'backend\src\NMS.Api'

Write-Host ''
Write-Host 'Paste your Groq API key (it starts with gsk_) and press Enter.' -ForegroundColor Cyan
Write-Host '(Right-click or Ctrl+V to paste. Nothing appears while you paste; that is normal.)'
$secure = Read-Host -AsSecureString 'Groq API key'
$key = [System.Net.NetworkCredential]::new('', $secure).Password.Trim()
if ($key -notmatch '^gsk_[A-Za-z0-9]{20,}$') {
  Write-Host 'That does not look like a Groq key (it should start with gsk_). Nothing was changed.' -ForegroundColor Red
  exit 1
}

dotnet user-secrets set 'Ai:Groq:ApiKey' $key --project $api | Out-Null
$key = $null
Write-Host 'Key saved in your local secrets.' -ForegroundColor Green

# The API reads the key when it starts, so restart the local one if it is running.
$running = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*NMS.Api.dll*' }
if ($running) {
  $out = Join-Path $root '.run\api'
  $running | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
  Start-Sleep -Seconds 2
  $env:ASPNETCORE_ENVIRONMENT = 'Development'
  Start-Process dotnet -ArgumentList 'NMS.Api.dll', '--urls', 'http://0.0.0.0:5178' -WorkingDirectory $out -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $env:TEMP 'nms-api.log') -RedirectStandardError (Join-Path $env:TEMP 'nms-api.err.log')
  for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 2
    try { Invoke-WebRequest http://localhost:5178/health/ready -UseBasicParsing -TimeoutSec 3 | Out-Null; break } catch {}
  }
  Write-Host 'The API has restarted with the new key. Try "Summarise attendance" again.' -ForegroundColor Green
} else {
  Write-Host 'The API is not running; it will use the key the next time it starts.'
}
