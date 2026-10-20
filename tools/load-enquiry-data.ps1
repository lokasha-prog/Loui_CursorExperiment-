<#
  Adds test enquiries, tours and follow-ups to a LOCAL development system through the API (as if typed in).
  About ten families at different stages and from different sources, a few booked and completed tours, a blocked-off lunch hour,
  and one enquiry converted into a child. Names and numbers are made up. Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-enquiry-data.ps1
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

$staff = @(Call 'GET' '/staff?limit=100&status=active')
$programs = @(Call 'GET' '/programs?limit=100')
$sources = @('walk_in', 'website', 'referral', 'social_media', 'phone', 'email', 'existing_parent')
$families = @(
  @('Al Mazrouei', 'Hessa Al Mazrouei', 14), @('Thompson', 'Oliver Thompson', 20), @('Nasser', 'Layan Nasser', 8), @('Joseph', 'Anna Joseph', 26),
  @('Al Hashimi', 'Rashid Al Hashimi', 11), @('Sharma', 'Aarav Sharma', 17), @('Boulos', 'Elias Boulos', 5), @('Al Ali', 'Mouza Al Ali', 30),
  @('Rahimi', 'Sina Rahimi', 22), @('Kowalski', 'Zofia Kowalski', 13)
)
$ids = @()
$n = 0
foreach ($f in $families) {
    $r = Try-Call 'POST' '/enquiries' @{
        family_name = $f[0]; child_name = $f[1]; phone = "+9715010$('{0:D5}' -f (20000 + $n))"; email = "$($f[0].ToLower().Replace(' ', ''))$n@example.com"
        preferred_contact = @('phone', 'whatsapp', 'email')[$n % 3]; child_date_of_birth = (Get-Date).AddMonths(-$f[2]).ToString('yyyy-MM-dd'); source = $sources[$n % 7]
        program_id = if ($programs.Count -gt 0) { $programs[$n % $programs.Count].id } else { $null }
        assigned_staff_id = if ($staff.Count -gt 0) { $staff[$n % $staff.Count].id } else { $null }
        desired_start_date = (Get-Date).AddMonths(1 + ($n % 3)).ToString('yyyy-MM-01'); notes = 'Test enquiry'
    }
    if ($r) { $ids += $r.enquiry.id }
    $n++
}

# Contact log: the first six families have been spoken to.
$kinds = @('call', 'whatsapp', 'email', 'visit')
for ($i = 0; $i -lt [Math]::Min(6, $ids.Count); $i++) {
    Try-Call 'POST' "/enquiries/$($ids[$i])/activities" @{ kind = $kinds[$i % 4]; summary = 'Spoke about fees, hours and availability.' } | Out-Null
}
if ($ids.Count -gt 6) { Try-Call 'POST' "/enquiries/$($ids[6])/actions/change-stage" @{ stage = 'interested'; reason = 'Asked for a brochure' } | Out-Null }
if ($ids.Count -gt 9) { Try-Call 'POST' "/enquiries/$($ids[9])/actions/archive" | Out-Null }

# Tours on the coming days, a lunch block-off, and one completed tour.
$tourIds = @()
for ($i = 0; $i -lt [Math]::Min(4, $ids.Count); $i++) {
    $when = (Get-Date).Date.AddDays(1 + $i).AddHours(9 + $i).ToUniversalTime().ToString('o')
    $t = Try-Call 'POST' '/tours' @{ enquiry_id = $ids[$i]; starts_at = $when; duration_minutes = 45; staff_id = if ($staff.Count -gt 0) { $staff[$i % $staff.Count].id } else { $null }; tour_type = @('general', 'program_focus', 'general', 'birthday_party')[$i]; adults = 2; children = 1 }
    if ($t) { $tourIds += $t.id }
}
if ($staff.Count -gt 0) {
    Try-Call 'POST' '/tours/block-offs' @{ staff_id = $staff[0].id; starts_at = (Get-Date).Date.AddDays(2).AddHours(12).ToUniversalTime().ToString('o'); ends_at = (Get-Date).Date.AddDays(2).AddHours(13).ToUniversalTime().ToString('o'); reason = 'Lunch' } | Out-Null
}
if ($tourIds.Count -gt 0) { Try-Call 'POST' "/tours/$($tourIds[0])/actions/set-status" @{ status = 'completed' } | Out-Null }

# One family enrols: the enquiry becomes a signed-up child with a parent contact.
if ($ids.Count -gt 4) { Try-Call 'POST' "/enquiries/$($ids[4])/actions/convert" @{} | Out-Null }
Write-Host 'Done: enquiries, contact logs, tours, a block-off, a follow-up task and one enrolment loaded.' -ForegroundColor Green
