<#
  Adds test forms, answers, admission steps and printed certificates to a LOCAL development system through the API, as if made on screen.
  Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-forms-data.ps1
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

function F($id, $type, $en, $ar, $required = $false, $options = @(), $when = $null, $equals = $null) {
    @{ id = $id; type = $type; label = @{ en = $en; ar = $ar }; required = $required; options = @($options | ForEach-Object { @{ value = $_[0]; label = @{ en = $_[1]; ar = $_[2] } } }); show_when_field_id = $when; show_when_equals = $equals }
}
function NewForm($en, $ar, $type, $fields, $public = $false) {
    $made = Try-Call 'POST' '/forms' @{ name = @{ en = $en; ar = $ar }; type = $type; is_public = $public }
    if (-not $made) { return $null }
    $id = $made.form.id
    Try-Call 'PUT' "/forms/$id/fields" @{ fields = $fields } | Out-Null
    Try-Call 'POST' "/forms/$id/actions/publish" @{} | Out-Null
    $id
}

$kids = @(Call 'GET' '/students?limit=100&status=active')
$permission = NewForm 'Trip permission slip' 'إذن الرحلة' 'custom' @(
    (F 'child' 'text' 'Child name' 'اسم الطفل' $true), (F 'trip' 'dropdown' 'Which trip' 'أي رحلة' $true @(@('farm', 'Farm visit', 'زيارة المزرعة'), @('museum', 'Museum', 'المتحف'))),
    (F 'allergy' 'radio' 'Any allergies?' 'هل توجد حساسية؟' $true @(@('no', 'No', 'لا'), @('yes', 'Yes', 'نعم'))),
    (F 'allergy_detail' 'textarea' 'Please describe' 'يرجى الوصف' $true @() 'allergy' 'yes'), (F 'agree' 'checkbox' 'I give permission' 'أعطي الإذن' $true))
$incident = NewForm 'Incident report' 'تقرير حادثة' 'incident' @(
    (F 'when' 'date' 'Date' 'التاريخ' $true), (F 'what' 'textarea' 'What happened' 'ماذا حدث' $true), (F 'injury' 'checkbox' 'Was anyone hurt' 'هل أصيب أحد'),
    (F 'action' 'textarea' 'Action taken' 'الإجراء المتخذ' $false @() 'injury' 'true'))
$feedback = NewForm 'Parent feedback' 'ملاحظات أولياء الأمور' 'custom' @(
    (F 'rating' 'radio' 'How was your week' 'كيف كان أسبوعكم' $true @(@('great', 'Great', 'ممتاز'), @('ok', 'Okay', 'جيد'), @('poor', 'Poor', 'ضعيف'))), (F 'comment' 'textarea' 'Comments' 'تعليقات')) $true
$checkout = NewForm 'Late pick-up note' 'ملاحظة الاستلام المتأخر' 'check_in_out' @((F 'reason' 'text' 'Reason' 'السبب' $true), (F 'minutes' 'number' 'Minutes late' 'دقائق التأخير'))

# About ten answers, some for children.
$n = 0
foreach ($k in $kids | Select-Object -First 4) {
    if ($permission) { Try-Call 'POST' "/forms/$permission/submissions" @{ answers = @{ child = "$($k.first_name) $($k.last_name)"; trip = @('farm', 'museum')[$n % 2]; allergy = @('no', 'yes')[$n % 2]; allergy_detail = if ($n % 2) { 'Peanuts' } else { $null }; agree = $true }; student_id = $k.id } | Out-Null }
    $n++
}
if ($incident) {
    Try-Call 'POST' "/forms/$incident/submissions" @{ answers = @{ when = (Get-Date).ToString('yyyy-MM-dd'); what = 'Fell on the playground, no injury'; injury = $false } } | Out-Null
    Try-Call 'POST' "/forms/$incident/submissions" @{ answers = @{ when = (Get-Date).AddDays(-2).ToString('yyyy-MM-dd'); what = 'Scraped knee'; injury = $true; action = 'Cleaned and plaster applied' }; student_id = $kids[0].id } | Out-Null
}
if ($feedback) { foreach ($r in 'great', 'great', 'ok', 'poor') { Try-Call 'POST' "/forms/$feedback/submissions" @{ answers = @{ rating = $r; comment = 'Thank you' } } | Out-Null } }
if ($checkout) { Try-Call 'POST' "/forms/$checkout/submissions" @{ answers = @{ reason = 'Traffic'; minutes = 20 } } | Out-Null }

# Admission steps (the first one sends the permission slip) and four children entering the process.
$steps = @(Call 'GET' '/admissions/steps')
foreach ($s in @(@('Submit medical records', 'تقديم السجلات الطبية', 7), @('Sign the contract', 'توقيع العقد', 10), @('Trial day', 'يوم تجريبي', 14), @('Confirm enrolment', 'تأكيد التسجيل', 3))) {
    Try-Call 'POST' '/admissions/steps' @{ name = @{ en = $s[0]; ar = $s[1] }; form_id = $null; due_days = $s[2] } | Out-Null
}
foreach ($k in $kids | Select-Object -Skip 4 -First 4) { Try-Call 'POST' "/admissions/students/$($k.id)/actions/start" @{} | Out-Null }

# Certificates for five children, one design each.
$ids = @($kids | Select-Object -First 5 | ForEach-Object { $_.id })
foreach ($t in 'achievement-gold', 'star-student', 'good-behaviour', 'reading-champion', 'artist') {
    try {
        Invoke-WebRequest -Uri "$Api/api/v1/tools/certificates" -Method POST -WebSession $session -Headers $headers -ContentType 'application/json; charset=utf-8' -UseBasicParsing `
            -Body ([System.Text.Encoding]::UTF8.GetBytes((@{ template = $t; student_ids = $ids; signatures = @(@{ name = 'Salam Zabalawi'; title = 'Centre director' }) } | ConvertTo-Json -Depth 5 -Compress))) | Out-Null
    } catch { Write-Host "  (certificate $t skipped)" -ForegroundColor DarkYellow }
}
Write-Host 'Done: forms, answers, admission steps, children in admission and certificates loaded.' -ForegroundColor Green
