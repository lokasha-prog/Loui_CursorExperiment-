@echo off
setlocal EnableExtensions
rem Builds the web app, publishes the API into .run\api, copies the build into wwwroot, and restarts the API.
rem Optional argument: lan  — listen on http://0.0.0.0:5178 so phones on the same network can reach it.
cd /d "%~dp0.."
set "ROOT=%CD%"
set "OUT=%ROOT%\.run\api"
set "URLS=http://localhost:5178"
if /I "%~1"=="lan" set "URLS=http://0.0.0.0:5178"

echo [1/4] Building web
pushd "%ROOT%\web"
call pnpm build
if errorlevel 1 (
  echo Web build failed.
  popd
  exit /b 1
)
popd

echo [2/4] Stopping API
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*NMS.Api.dll*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }; Start-Sleep -Seconds 2"
if errorlevel 1 (
  echo Failed to stop the API.
  exit /b 1
)

echo [3/4] Publishing API and copying the rendered web app
dotnet publish "%ROOT%\backend\src\NMS.Api" -c Debug -o "%OUT%" --nologo -v q
if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)
if exist "%OUT%\wwwroot" rmdir /s /q "%OUT%\wwwroot"
robocopy "%ROOT%\web\dist" "%OUT%\wwwroot" /E /NFL /NDL /NJH /NJS /nc /ns /np
if errorlevel 8 (
  echo Copy of the rendered web app failed.
  exit /b 1
)

echo [4/4] Starting API on %URLS%
powershell -NoProfile -Command "$env:ASPNETCORE_ENVIRONMENT='Development'; Start-Process -FilePath dotnet -ArgumentList 'NMS.Api.dll','--urls','%URLS%' -WorkingDirectory '%OUT%' -WindowStyle Hidden -RedirectStandardOutput (Join-Path $env:TEMP 'nms-api.log') -RedirectStandardError (Join-Path $env:TEMP 'nms-api.err.log')"
if errorlevel 1 (
  echo Failed to start the API.
  exit /b 1
)

echo Waiting for http://localhost:5178/health/ready
powershell -NoProfile -Command "Start-Sleep -Seconds 20; (Invoke-WebRequest -UseBasicParsing http://localhost:5178/health/ready).Content"
if errorlevel 1 (
  echo API did not become ready. See %TEMP%\nms-api.err.log
  exit /b 1
)

echo Ready: http://localhost:5178
exit /b 0
