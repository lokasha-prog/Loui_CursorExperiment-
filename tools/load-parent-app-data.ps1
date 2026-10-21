<#
  Adds parent phone app test data to a LOCAL development system through the API: announcements, broadcast messages and phone registrations a parent would see.
  Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-parent-app-data.ps1
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

# Sign in through the local-only test-user helper.
$login = Call 'POST' '/dev/quick-login' @{ role = 'it' }
$headers['Authorization'] = "Bearer $($login.access_token)"
$headers['X-NMS-Client'] = 'mobile'
Write-Host 'Signed in as the local test IT administrator (phone-app style).'

# What parents see in the phone app: announcements in the feed, centre-wide messages, and this phone registered for alerts.
$count = 0
$news = @(
    @{ en = 'Sports day is next Thursday. Please send comfortable shoes and a water bottle.'; ar = 'يوم الرياضة يوم الخميس القادم. يرجى إرسال حذاء مريح وزجاجة ماء.' },
    @{ en = 'The nursery will close early on Wednesday at 12:00 for staff training.'; ar = 'ستغلق الحضانة مبكرا يوم الأربعاء الساعة 12:00 للتدريب.' },
    @{ en = 'New healthy snack menu starts this week.'; ar = 'تبدأ قائمة الوجبات الخفيفة الصحية الجديدة هذا الأسبوع.' },
    @{ en = 'Photos from the art morning are now in the feed.'; ar = 'صور صباح الفن متاحة الآن في الخلاصة.' },
    @{ en = 'Please update your emergency contact numbers at reception.'; ar = 'يرجى تحديث أرقام الطوارئ لدى الاستقبال.' }
)
foreach ($n in $news) {
    if (Try-Call 'POST' '/activities' @{ type = 'announcement'; caption = @{ en = $n.en; ar = $n.ar }; student_ids = @(); media_ids = @() }) { $count++ }
}
$messages = @(
    @{ subject = 'Welcome to the new app'; body = 'You can now follow your child''s day, read reports and message us from your phone.' },
    @{ subject = 'Fee reminder'; body = 'Term invoices were sent this week. Please contact the office if you have questions.' },
    @{ subject = 'Weather update'; body = 'Outdoor play is moved to the morning because of the heat in the afternoon.' }
)
foreach ($m in $messages) {
    if (Try-Call 'POST' '/messages/threads' @{ subject = $m.subject; body = $m.body; broadcast = $true }) { $count++ }
}
$parent = Call 'POST' '/dev/quick-login' @{ role = 'parent' }
$headers['Authorization'] = "Bearer $($parent.access_token)"
foreach ($i in 1..2) {
    if (Try-Call 'POST' '/notifications/devices' @{ token = "ExponentPushToken[test-parent-phone-$i]"; platform = 'android' }) { $count++ }
}
Write-Host "Done: $count parent-app records added through the API."
