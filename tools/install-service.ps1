<#
  Installs the NMS system as a Windows service on the computer that runs SQL Server (run as Administrator).
  Secrets are stored in machine environment variables for the service, never in files that are committed.
  Usage: .\install-service.ps1 -Url http://0.0.0.0:8080 -SqlInstance '.\SQLEXPRESS' -Database NMS
#>
param(
    [string]$Url = 'http://0.0.0.0:8080',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [string]$Database = 'NMS',
    [string]$ServiceName = 'NMS',
    [string]$SmtpHost = '', [int]$SmtpPort = 25, [string]$SmtpFrom = ''
)
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
function NewKey($bytes) { [Convert]::ToBase64String((1..$bytes | ForEach-Object { Get-Random -Maximum 256 } | ForEach-Object { [byte]$_ })) }

# Keys are generated once and kept; running this again for an upgrade reuses them.
$keyFile = Join-Path $dir 'nms.keys.xml'
if (-not (Test-Path $keyFile)) {
    @{ Jwt = (NewKey 48); Encryption = (NewKey 32); Setup = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N') } | Export-Clixml $keyFile
    icacls $keyFile /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" | Out-Null
    Write-Host "Keys created in $keyFile. BACK THIS FILE UP: the encryption key cannot be recovered without it." -ForegroundColor Yellow
}
$k = Import-Clixml $keyFile

$env_ = @(
    "ASPNETCORE_URLS=$Url",
    "ConnectionStrings__Nms=Server=$SqlInstance;Database=$Database;Integrated Security=true;TrustServerCertificate=true;Encrypt=false",
    "Jwt__SigningKey=$($k.Jwt)", "Encryption__Key=$($k.Encryption)", "Auth__BootstrapToken=$($k.Setup)",
    'Database__MigrateOnStartup=true'
)
if ($SmtpHost) { $env_ += "Smtp__Host=$SmtpHost", "Smtp__Port=$SmtpPort", "Smtp__From=$SmtpFrom" }

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) { Stop-Service $ServiceName; sc.exe delete $ServiceName | Out-Null; Start-Sleep 2 }
New-Service -Name $ServiceName -DisplayName 'Bright Futures NMS' -BinaryPathName "`"$(Join-Path $dir 'NMS.Api.exe')`"" -StartupType Automatic -Description 'Nursery management system' | Out-Null
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment -Value $env_ -Type MultiString
Start-Service $ServiceName
Write-Host "Service '$ServiceName' started on $Url." -ForegroundColor Green
Write-Host "First-time setup key (needed once on the setup screen): $($k.Setup)" -ForegroundColor Yellow
