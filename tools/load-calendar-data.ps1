<#
  Adds test calendar events to a LOCAL development system through the API, as if made on the Calendar screen:
  holidays, outings, a staff meeting, and a few repeating events. Dates are relative to today. Nothing ships with the app;
  drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-calendar-data.ps1
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

$rooms = @(Call 'GET' '/rooms?limit=100')
function At($days, $hour, $minute = 0) { (Get-Date).Date.AddDays($days).AddHours($hour).AddMinutes($minute).ToUniversalTime().ToString('o') }
$until = (Get-Date).AddMonths(3).ToString('yyyyMMdd')
$events = @(
  @{ en = 'Eid Al Fitr holiday'; ar = 'عطلة عيد الفطر'; type = 'holiday'; allDay = $true; from = 20; to = 23 },
  @{ en = 'Parents open morning'; ar = 'صباح مفتوح لأولياء الأمور'; type = 'regular'; from = 6; to = 6; h = 9; len = 2 },
  @{ en = 'Staff training day'; ar = 'يوم تدريب الموظفين'; type = 'staff_only'; from = 9; to = 9; h = 8; len = 6 },
  @{ en = 'Trip to the farm'; ar = 'رحلة إلى المزرعة'; type = 'regular'; from = 14; to = 14; h = 9; len = 4; room = $true },
  @{ en = 'Photo day'; ar = 'يوم التصوير'; type = 'regular'; from = 17; to = 17; h = 10; len = 3 },
  @{ en = 'National Day celebration'; ar = 'احتفال اليوم الوطني'; type = 'regular'; from = 28; to = 28; h = 9; len = 3 },
  @{ en = 'Winter break'; ar = 'العطلة الشتوية'; type = 'holiday'; allDay = $true; from = 45; to = 59 },
  @{ en = 'Storytelling circle'; ar = 'حلقة الحكايات'; type = 'regular'; from = 2; to = 2; h = 10; len = 1; rule = "FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=$until" },
  @{ en = 'Weekly staff meeting'; ar = 'اجتماع الموظفين الأسبوعي'; type = 'staff_only'; from = 3; to = 3; h = 14; len = 1; rule = "FREQ=WEEKLY;BYDAY=TH;UNTIL=$until" },
  @{ en = 'Healthy snack day'; ar = 'يوم الوجبة الصحية'; type = 'regular'; from = 5; to = 5; h = 10; len = 1; rule = "FREQ=MONTHLY;COUNT=4" }
)
$made = 0
foreach ($e in $events) {
    $body = @{
        title = @{ en = $e.en; ar = $e.ar }; description = @{ en = 'Test event'; ar = 'فعالية تجريبية' }; type = $e.type; is_all_day = [bool]$e.allDay
        starts_at = if ($e.allDay) { At $e.from 0 } else { At $e.from $e.h }
        ends_at = if ($e.allDay) { At $e.to 23 59 } else { At $e.to ($e.h + $e.len) }
        room_id = if ($e.room -and $rooms.Count -gt 0) { $rooms[0].id } else { $null }
        visibility = if ($e.type -eq 'staff_only') { 'staff' } else { 'parents' }; recurrence = $e.rule
    }
    if (Try-Call 'POST' '/calendar/events' $body) { $made++ }
}
Write-Host "Done: $made calendar events loaded." -ForegroundColor Green
