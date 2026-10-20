<#
  Adds test custom roles (and a few people on them) to a LOCAL development system through the API, as if made in Settings.
  Passwords are random and thrown away; use the test-user buttons on the login screen to try the built-in roles.
  Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-settings-data.ps1
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
function Password { -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 22 | ForEach-Object { [char]$_ }) + 'aA1' }

$roles = @(
  @('Nursery manager', 'مدير الحضانة', @('students.read', 'students.write', 'staff.read', 'attendance.read', 'attendance.write', 'reports.view', 'enquiries.read', 'enquiries.write', 'tours.read', 'tours.write')),
  @('Billing clerk', 'موظف الفواتير', @('billing.read', 'billing.write', 'payments.record', 'invoices.generate', 'reports.view')),
  @('Open day host', 'مضيف اليوم المفتوح', @('enquiries.read', 'tours.read', 'tours.write')),
  @('Marketing assistant', 'مساعد التسويق', @('enquiries.read', 'enquiries.write', 'messaging.read', 'messaging.broadcast')),
  @('Kitchen staff', 'طاقم المطبخ', @('students.read', 'attendance.read')),
  @('Bus supervisor', 'مشرف الحافلة', @('students.read', 'attendance.read', 'attendance.write')),
  @('Auditor (read only)', 'مدقق (قراءة فقط)', @('students.read', 'staff.read', 'billing.read', 'reports.view', 'audit_log.view')),
  @('Photographer', 'مصور', @('activities.read', 'activities.write', 'students.read')),
  @('Nurse', 'ممرضة', @('students.read', 'attendance.read', 'activities.read')),
  @('Trainee teacher', 'معلمة متدربة', @('students.read', 'attendance.read', 'activities.read', 'activities.write'))
)
$made = @()
foreach ($r in $roles) {
    $role = Try-Call 'POST' '/roles' @{ display_name = @{ en = $r[0]; ar = $r[1] }; description = 'Test role'; permissions = $r[2] }
    if ($role) { $made += $role }
}

# A few people on the new roles, so the permissions can be tried by signing in as them.
$n = 0
foreach ($role in $made | Select-Object -First 3) {
    $n++
    Try-Call 'POST' '/users' @{ email = "test.role$n.$(Get-Random -Maximum 9999)@dev.local"; first_name = 'Test'; last_name = $role.display_name.en; password = (Password); roles = @($role.name); preferred_language = 'en' } | Out-Null
}
Write-Host "Done: $($made.Count) custom roles and $n people on them loaded." -ForegroundColor Green
