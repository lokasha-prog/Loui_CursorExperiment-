<#
  Backs up the NMS database (full backup, compressed, checked) and deletes backups older than the keep period.
  Schedule it daily:  schtasks /Create /TN "NMS backup" /SC DAILY /ST 02:00 /RU SYSTEM /TR "powershell -NoProfile -File C:\NMS\backup-db.ps1"
  Restore:            see PILOT-RUNBOOK.md ("Restoring a backup").
#>
param([string]$SqlInstance = '.\SQLEXPRESS', [string]$Database = 'NMS', [string]$Folder = 'D:\NMS-backups', [int]$KeepDays = 30)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Folder | Out-Null
# The SQL Server service account (NT SERVICE\MSSQL$SQLEXPRESS) must be allowed to write to this folder.
$file = Join-Path $Folder ("{0}_{1:yyyyMMdd_HHmm}.bak" -f $Database, (Get-Date))
sqlcmd -S $SqlInstance -E -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$file' WITH INIT, CHECKSUM, STATS = 10"
if ($LASTEXITCODE -ne 0) { throw 'Backup failed' }
sqlcmd -S $SqlInstance -E -b -Q "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM"
if ($LASTEXITCODE -ne 0) { throw 'Backup could not be verified' }
Get-ChildItem $Folder -Filter "$($Database)_*.bak" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Force
Write-Host "Backup OK: $file"
