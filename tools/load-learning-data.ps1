<#
  Loads a starter EYFS curriculum and some lessons into a LOCAL development system through the API, as if entered on screen.
  Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-learning-data.ps1
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
Write-Host 'Signed in as the local test IT administrator.'

# 1. The curriculum: a starter EYFS set (7 areas, 6 age bands, 42 milestones in English and Arabic) from tools\data\eyfs-starter.json.
$frameworks = @(Call 'GET' '/learning/frameworks')
$eyfs = $frameworks | Where-Object { $_.code -eq 'eyfs' } | Select-Object -First 1
if (-not $eyfs) {
    $arName = [string]::Join('', [char[]](0x0645,0x0631,0x062D,0x0644,0x0629,0x0020,0x0627,0x0644,0x062A,0x0623,0x0633,0x064A,0x0633,0x0020,0x0644,0x0644,0x0633,0x0646,0x0648,0x0627,0x062A,0x0020,0x0627,0x0644,0x0645,0x0628,0x0643,0x0631,0x0629))
    $eyfs = Call 'POST' '/learning/frameworks' @{ code = 'eyfs'; name = @{ en = 'Early Years Foundation Stage (EYFS)'; ar = $arName } }
}
$file = Join-Path $PSScriptRoot 'data\eyfs-starter.json'
$json = [System.IO.File]::ReadAllBytes($file)
$import = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Api/api/v1/learning/frameworks/$($eyfs.id)/import" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $json
$tree = ($import.Content | ConvertFrom-Json).data
Write-Host "Curriculum ready: $($tree.areas.Count) areas, $($tree.age_bands.Count) age bands, $($tree.milestones.Count) milestones."

# 2. Lessons planned from it.
$area = @{}; $tree.areas | ForEach-Object { $area[$_.code] = $_.id }
$band = @{}; $tree.age_bands | ForEach-Object { $band[$_.code] = $_.id }
$ms = @{}; $tree.milestones | ForEach-Object { $ms[$_.code] = $_.id }
$existing = @((Call 'GET' '/learning/lessons?limit=100')).Count
$lessons = @(
    @('Rainbow painting', 'ar:رسم قوس قزح', 'ead', 'b30-50', 40, @('Washable paint', 'Large paper', 'Aprons'), @('Name the colours', 'Mix two colours')),
    @('Sensory tray: water and sponges', 'ar:صينية حسية: ماء وإسفنج', 'pd', 'b16-26', 25, @('Tray', 'Sponges', 'Cups'), @('Squeeze and pour', 'Explore wet and dry')),
    @('Story time: the hungry caterpillar', 'ar:وقت القصة: اليرقة الجائعة', 'l', 'b22-36', 30, @('Picture book', 'Puppet'), @('Join in with repeated phrases', 'Point to pictures')),
    @('Counting with stones', 'ar:العد بالحصى', 'm', 'b30-50', 30, @('Smooth stones', 'Number cards', 'Baskets'), @('Count to ten', 'Match numerals to groups')),
    @('Our family tree', 'ar:شجرة عائلتنا', 'uw', 'b40-60', 45, @('Photos from home', 'Card', 'Glue'), @('Talk about family', 'Understand past and present')),
    @('Feelings faces', 'ar:وجوه المشاعر', 'psed', 'b22-36', 25, @('Mirror', 'Feelings cards'), @('Name two feelings', 'Show how others feel')),
    @('Peek-a-boo and name songs', 'ar:اختبئ وظهر وأغاني الأسماء', 'cal', 'b8-20', 20, @('Scarves', 'Mirror'), @('Respond to their name', 'Make sounds back')),
    @('Obstacle course', 'ar:مسار العقبات', 'pd', 'b40-60', 35, @('Cones', 'Hoops', 'Balance beam'), @('Move with control', 'Balance and jump')),
    @('Shape hunt in the garden', 'ar:البحث عن الأشكال في الحديقة', 'm', 'b22-36', 30, @('Shape cards', 'Clipboards'), @('Name circle, square, triangle')),
    @('Drum circle', 'ar:حلقة الطبول', 'ead', 'b16-26', 20, @('Hand drums', 'Shakers'), @('Join in with songs', 'Tap to make sounds')),
    @('Planting seeds', 'ar:زراعة البذور', 'uw', 'b30-50', 40, @('Seeds', 'Pots', 'Compost', 'Watering cans'), @('Talk about plants', 'Notice change over time'))
)
$made = 0
foreach ($l in $lessons) {
    $arTitle = $l[1].Substring(3)
    $title = @{ en = $l[0]; ar = $arTitle }
    $code = "$($l[2])-$($l[3])"
    $body = @{
        title = $title; framework_id = $eyfs.id; area_id = $area[$l[2]]; age_band_id = $band[$l[3]]; duration_minutes = $l[4]
        materials = $l[5]; objectives = $l[6]; milestone_ids = @($ms[$code])
    }
    if (Try-Call 'POST' '/learning/lessons' $body) { $made++ }
}
Write-Host "Done: curriculum loaded and $made lessons added (there were $existing before)."
