<#
  Adds test billing data to a LOCAL development system through the API (as if typed in): assigns children to the fee plans,
  creates about ten invoices with a mix of paid, part-paid and unpaid, then runs the automatic monthly invoice generation.
  Figures are made up. Nothing here ships with the app. Remove it all by dropping the NMS database.

  Usage:  powershell -File tools\load-billing-data.ps1
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

$plans = @(Call 'GET' '/fee-plans?limit=50')
if ($plans.Count -eq 0) { throw 'Create at least one fee plan first.' }
$kids = @(Call 'GET' '/students?limit=100')
Write-Host "Found $($plans.Count) fee plans and $($kids.Count) children."

# Put every child on a plan (alternating), skipping those already on one.
$i = 0
foreach ($k in $kids) {
    $plan = $plans[$i % $plans.Count]; $i++
    Try-Call 'POST' "/fee-plans/$($plan.id)/students" @{ student_ids = @($k.id); effective_from = (Get-Date -Day 1).ToString('yyyy-MM-dd') } | Out-Null
}

# About ten hand-written invoices of different kinds; some paid in full, some in part, some left unpaid.
$items = @(
  @{ en = 'Registration fee'; ar = 'رسوم التسجيل'; price = 500 }, @{ en = 'Uniform'; ar = 'الزي المدرسي'; price = 320 },
  @{ en = 'Trip to the farm'; ar = 'رحلة إلى المزرعة'; price = 85 }, @{ en = 'Art supplies'; ar = 'أدوات الرسم'; price = 120 },
  @{ en = 'Extra hours'; ar = 'ساعات إضافية'; price = 260 }
)
$methods = @('cash', 'card', 'bank_transfer', 'cheque')
$n = 0
foreach ($k in $kids | Select-Object -First 10) {
    $it = $items[$n % $items.Count]
    $inv = Try-Call 'POST' '/invoices' @{
        student_id = $k.id; period_start = (Get-Date).ToString('yyyy-MM-dd'); period_end = (Get-Date).ToString('yyyy-MM-dd'); issue = $true
        lines = @(@{ description = @{ en = $it.en; ar = $it.ar }; quantity = 1; unit_price_fils = [long]($it.price * 100); tax_rate_percent = 5 })
    }
    if ($inv) {
        $total = [long]$inv.invoice.total_fils
        $paid = switch ($n % 3) { 0 { $total } 1 { [long]($total / 2) } default { 0 } }
        if ($paid -gt 0) {
            Try-Call 'POST' "/invoices/$($inv.invoice.id)/payments" @{ amount_fils = $paid; payment_date = (Get-Date).ToString('yyyy-MM-dd'); method = $methods[$n % 4]; reference = "TEST-$(1000 + $n)" } | Out-Null
        }
    }
    $n++
}

# The same step the daily job performs: monthly invoices from fee plans.
Try-Call 'POST' '/invoices/actions/generate' @{} | Out-Null

# About ten expenses over the last few months, so the net result and the expense report have something to show.
$spend = @(
  @('rent', 12000, 'Monthly rent'), @('utilities', 1450, 'Electricity and water'), @('salaries', 38000, 'Staff salaries'), @('supplies', 640, 'Craft and cleaning supplies'),
  @('food', 2100, 'Kitchen groceries'), @('other', 300, 'Fire extinguisher check'), @('supplies', 980, 'Books and toys'), @('food', 1750, 'Snacks and lunch ingredients'),
  @('utilities', 520, 'Internet and phones'), @('rent', 12000, 'Monthly rent')
)
$e = 0
foreach ($x in $spend) {
    $when = (Get-Date).AddDays(-($e * 9)).ToString('yyyy-MM-dd')
    Try-Call 'POST' '/expenses' @{ expense_date = $when; amount_fils = [long]($x[1] * 100); category = $x[0]; description = $x[2]; receipt_media_id = $null } | Out-Null
    $e++
}

# Send any reminders that are due today (the daily job does this on its own).
Try-Call 'POST' '/billing/reminders/actions/run' @{} | Out-Null
Write-Host 'Done: children placed on fee plans, test invoices, payments and expenses loaded, monthly invoices generated, reminders run.' -ForegroundColor Green
