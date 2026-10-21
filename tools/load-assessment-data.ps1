<#
  Adds test observations, assessment checklists and assessments to a LOCAL development system through the API, as if entered on screen.
  Run load-learning-data.ps1 first. Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-assessment-data.ps1
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

$login = Call 'POST' '/dev/quick-login' @{ role = 'it' }
$headers['Authorization'] = "Bearer $($login.access_token)"
Write-Host 'Signed in as the local test IT administrator.'

$eyfs = @(Call 'GET' '/learning/frameworks') | Where-Object { $_.code -eq 'eyfs' } | Select-Object -First 1
if (-not $eyfs) { throw 'Load the curriculum first: powershell -File tools\load-learning-data.ps1' }
$tree = Call 'GET' "/learning/frameworks/$($eyfs.id)"
$students = @(Call 'GET' '/students?limit=12&status=active')
if ($students.Count -lt 4) { throw 'Not enough children to load learning data.' }
$band = @{}; $tree.age_bands | ForEach-Object { $band[$_.code] = $_.id }
$area = @{}; $tree.areas | ForEach-Object { $area[$_.code] = $_.id }
$count = 0

# Assessment checklists: one three-level checklist for 22-36 months and one yes/no checklist for 30-50 months.
$m2236 = @($tree.milestones | Where-Object { $_.age_band_id -eq $band['b22-36'] } | ForEach-Object { $_.id })
$m3050 = @($tree.milestones | Where-Object { $_.age_band_id -eq $band['b30-50'] } | ForEach-Object { $_.id })
$arToddler = [string]::Join('', [char[]](0x062A,0x0642,0x064A,0x064A,0x0645,0x0020,0x0627,0x0644,0x0623,0x0637,0x0641,0x0627,0x0644,0x0020,0x0627,0x0644,0x0635,0x063A,0x0627,0x0631))
$arPre = [string]::Join('', [char[]](0x062A,0x0642,0x064A,0x064A,0x0645,0x0020,0x0645,0x0627,0x0642,0x0628,0x0644,0x0020,0x0627,0x0644,0x0645,0x062F,0x0631,0x0633,0x0629))
$t1 = Try-Call 'POST' '/learning/assessment-templates' @{ name = @{ en = 'Toddler check (22-36 months)'; ar = $arToddler }; framework_id = $eyfs.id; age_band_id = $band['b22-36']; scoring_kind = 'three_point'; milestone_ids = $m2236 }
$t2 = Try-Call 'POST' '/learning/assessment-templates' @{ name = @{ en = 'Preschool check (30-50 months)'; ar = $arPre }; framework_id = $eyfs.id; age_band_id = $band['b30-50']; scoring_kind = 'binary'; milestone_ids = $m3050 }
if ($t1) { $count++ }; if ($t2) { $count++ }

# Observations: a mix of informal, formal and one concern, some shared with families.
$notes = @(
    @('informal', 'cal', 'b22-36', $true, 'Asked for more juice using two words.', 'ar:طلب مزيدا من العصير بكلمتين.'),
    @('informal', 'pd', 'b22-36', $true, 'Climbed the low steps and jumped down with a big smile.', 'ar:تسلق الدرجات المنخفضة وقفز وهو يبتسم.'),
    @('formal', 'm', 'b30-50', $false, 'Counted seven stones without help and matched them to the numeral.', 'ar:عد سبع حصى بدون مساعدة وطابقها مع الرقم.'),
    @('informal', 'ead', 'b30-50', $true, 'Mixed blue and yellow paint and shouted: green!', 'ar:مزج الطلاء الأزرق والأصفر وصاح: أخضر!'),
    @('informal', 'psed', 'b22-36', $true, 'Shared the blocks with a friend and waited for a turn.', 'ar:شارك المكعبات مع صديق وانتظر دوره.'),
    @('formal', 'l', 'b40-60', $false, 'Wrote the first letters of their name from memory.', 'ar:كتب أول حروف اسمه من الذاكرة.'),
    @('informal', 'uw', 'b30-50', $true, 'Watered the bean seeds and noticed the first shoot.', 'ar:سقى بذور الفول ولاحظ أول برعم.'),
    @('concern', 'cal', 'b22-36', $false, 'Very few words compared with peers; suggest a hearing check.', 'ar:كلمات قليلة جدا مقارنة بأقرانه؛ نقترح فحص السمع.'),
    @('informal', 'ead', 'b16-26', $true, 'Danced to the drum and copied the rhythm.', 'ar:رقص على الطبل وقلد الإيقاع.'),
    @('informal', 'pd', 'b40-60', $true, 'Caught a bounced ball three times in a row.', 'ar:التقط كرة مرتدة ثلاث مرات متتالية.')
)
$i = 0
foreach ($n in $notes) {
    $child = $students[$i % $students.Count]; $i++
    $body = @{ student_id = $child.id; type = $n[0]; framework_id = $eyfs.id; area_id = $area[$n[1]]; age_band_id = $band[$n[2]]; share_with_parents = $n[3]; notes = @{ en = $n[4]; ar = $n[5].Substring(3) } }
    if (Try-Call 'POST' '/learning/observations' $body) { $count++ }
}

# Assessments: score, complete and approve a few so the family version and the journey have content.
if ($t1) {
    foreach ($k in 0..2) {
        $child = $students[$k]
        $run = Try-Call 'POST' '/learning/assessments' @{ template_id = $t1.id; student_id = $child.id; term = 'Autumn 2026' }
        if (-not $run) { continue }
        $count++
        $scores = @($run.scores | ForEach-Object { @{ milestone_id = $_.milestone_id; score = (Get-Random -Minimum 1 -Maximum 3); note = '' } })
        $firstName = $child.first_name
        $summary = @{ en = "$firstName is growing in confidence and enjoys talking, climbing and playing with friends."; ar = [string]::Join('', [char[]](0x064A,0x0646,0x0645,0x0648,0x0020,0x0628,0x062B,0x0642,0x0629,0x0020,0x0648,0x064A,0x0633,0x062A,0x0645,0x062A,0x0639,0x0020,0x0628,0x0627,0x0644,0x0644,0x0639,0x0628,0x0020,0x0645,0x0639,0x0020,0x0623,0x0635,0x062F,0x0642,0x0627,0x0626,0x0647,0x002E)) }
        $null = Call 'PUT' "/learning/assessments/$($run.id)/scores" @{ scores = $scores; staff_summary = @{ en = 'Secure in most areas; keep encouraging two-word phrases.' }; parent_summary = $summary }
        $null = Call 'POST' "/learning/assessments/$($run.id)/actions/complete"
        if ($k -lt 2) { $null = Call 'POST' "/learning/assessments/$($run.id)/actions/approve" }
    }
}
Write-Host "Done: $count learning records (checklists, observations and assessments) added through the API."
