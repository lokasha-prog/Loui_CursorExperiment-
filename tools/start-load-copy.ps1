# Starts a second copy of the published API on http://localhost:5191 with the per-person request limits lifted, so speed and
# security tests are not slowed by the limiter (development helper; the real API on 5178 keeps its limits).
$root = Split-Path -Parent $PSScriptRoot
$copy = Join-Path $root '.run\api-load'
if (-not (Test-Path $copy)) { Copy-Item (Join-Path $root '.run\api') $copy -Recurse }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:RateLimiting__AuthenticatedPerMinute = '1000000'
$env:RateLimiting__AnonymousPerMinute = '1000000'
$env:RateLimiting__LoginPerMinute = '1000000'
$env:RateLimiting__SessionPerMinute = '1000000'
Start-Process -FilePath dotnet -ArgumentList 'NMS.Api.dll', '--urls', 'http://localhost:5191' -WorkingDirectory $copy -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $env:TEMP 'nms-api-load.log') -RedirectStandardError (Join-Path $env:TEMP 'nms-api-load.err.log')
Start-Sleep -Seconds 20
(Invoke-WebRequest -UseBasicParsing http://localhost:5191/health/ready).Content
