<#
  Requests a full set of test reports through the API (every report, in each file type it offers), as if made in the Report centre.
  They are made in the background; the files are kept for 30 days. Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-report-data.ps1
#>
param([string]$Api = 'http://localhost:5178')
$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$headers = @{}

function Call($method, $path, $body = $null) {
    $args = @{ Uri = "$Api/api/v1$path"; Method = $method; WebSession = $session; Headers = $headers; ContentType = 'application/json; charset=utf-8'; UseBasicParsing = $true }
    # Send real UTF-8 bytes: PowerShell 5.1 would otherwise turn Arabic letters into question marks.
    if ($null -ne $body) { $args.Body = [System.Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8 -Compress)) }
    for ($try = 0; $try -lt 6; $try++) {
        try { $r = Invoke-WebRequest @args; break } catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 429 -and $try -lt 5) { Start-Sleep -Seconds 15; continue }
            $failure = $_; break
        }
    }
    if (-not $r) {
        $detail = if ($failure.ErrorDetails) { $failure.ErrorDetails.Message } else { $failure.Exception.Message }
        throw "$method $path failed: $detail"
    }
    ($r.Content | ConvertFrom-Json).data
}
function Try-Call($method, $path, $body = $null) { try { Call $method $path $body } catch { Write-Host "  (skipped: $($_.Exception.Message.Substring(0, [Math]::Min(120, $_.Exception.Message.Length))))" -ForegroundColor DarkYellow; $null } }
function Password { -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 22 | ForEach-Object { [char]$_ }) + 'aA1' }

# Sign in through the local-only test-user helper. The IT role may create user accounts as well as everything else.
$login = Call 'POST' '/dev/quick-login' @{ role = 'it' }
$headers['Authorization'] = "Bearer $($login.access_token)"
Write-Host 'Signed in as the local test IT administrator.'

$month = (Get-Date).ToString('yyyy-MM')
$from = (Get-Date -Day 1 -Month 1).ToString('yyyy-MM-dd')
$to = (Get-Date).ToString('yyyy-MM-dd')
$plan = @(
  @('attendance_students', 'excel', @{ month = $month }), @('attendance_students', 'pdf', @{ month = $month }), @('attendance_staff', 'excel', @{ month = $month }), @('attendance_staff', 'pdf', @{ month = $month }),
  @('billing_revenue', 'excel', @{ from = $from; to = $to }), @('enquiry_conversion', 'excel', @{ from = $from; to = $to }), @('enquiry_conversion', 'pdf', @{ from = $from; to = $to }),
  @('adoption_parents', 'excel', @{ from = $from; to = $to }), @('adoption_staff', 'excel', @{ from = $from; to = $to }), @('audit_log', 'excel', @{ from = $from; to = $to }), @('audit_log', 'json', @{ from = $from; to = $to }),
  @('sign_in_sheet', 'pdf', @{ date = $to }), @('sign_in_sheet', 'pdf', @{ date = $to; language = 'ar' })
)
$made = 0
foreach ($p in $plan) {
    $lang = if ($p[2].language) { $p[2].language } else { 'en' }
    $filters = @{}; foreach ($k in $p[2].Keys) { if ($k -ne 'language') { $filters[$k] = $p[2][$k] } }
    if (Try-Call 'POST' '/reports/center/runs' @{ kind = $p[0]; format = $p[1]; parameters = $filters; language = $lang }) { $made++ }
}
Write-Host "Done: $made reports requested. They finish in the background (see Reports, Report centre)." -ForegroundColor Green
