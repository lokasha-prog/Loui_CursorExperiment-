# Rebuilds the API into .run\api and starts it on http://localhost:5178 (development helper).
# Runs the API from a copy so that building and testing never stop it.
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root '.run\api'

Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*NMS.Api.dll*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep -Seconds 2

dotnet publish (Join-Path $root 'backend\src\NMS.Api') -c Debug -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

$env:ASPNETCORE_ENVIRONMENT = 'Development'
Start-Process -FilePath dotnet -ArgumentList 'NMS.Api.dll', '--urls', 'http://localhost:5178' `
    -WorkingDirectory $out -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $env:TEMP 'nms-api.log') -RedirectStandardError (Join-Path $env:TEMP 'nms-api.err.log')
Start-Sleep -Seconds 20
(Invoke-WebRequest -UseBasicParsing http://localhost:5178/health/ready).Content
