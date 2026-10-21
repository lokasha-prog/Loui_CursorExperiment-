<#
  Adds staff phone app test data to a LOCAL development system through the API: check-ins/outs by card number and diary posts.
  Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-mobile-data.ps1
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

# Staff phone app data: arrivals and departures typed as the app sends them, plus diary posts shown in its Feed.
$students = @(Call 'GET' '/students?limit=12&status=active')
$count = 0
foreach ($s in ($students | Select-Object -First 6)) {
    if (Try-Call 'POST' '/attendance/actions/check-in' @{ barcode = $s.barcode }) { $count++ }
}
foreach ($s in ($students | Select-Object -Skip 3 -First 2)) {
    if (Try-Call 'POST' '/attendance/actions/check-out' @{ barcode = $s.barcode }) { $count++ }
}
$notes = @(
    @{ type = 'diary'; en = 'Story time: we read a book about the sea and drew fish.'; ar = 'وقت القصة: قرأنا كتابا عن البحر ورسمنا الأسماك.' },
    @{ type = 'food'; en = 'Snack: apple slices and yoghurt. Everyone enjoyed it.'; ar = 'وجبة خفيفة: شرائح التفاح والزبادي. استمتع الجميع بها.' },
    @{ type = 'mood'; en = 'A calm and happy morning in the room.'; ar = 'صباح هادئ وسعيد في الغرفة.' },
    @{ type = 'toilet'; en = 'Toilet routine completed after snack time.'; ar = 'تم الانتهاء من روتين دورة المياه بعد الوجبة.' }
)
foreach ($n in $notes) {
    if (Try-Call 'POST' '/activities' @{ type = $n.type; caption = @{ en = $n.en; ar = $n.ar }; student_ids = @(); media_ids = @() }) { $count++ }
}
Write-Host "Done: $count phone-app records added through the API."
