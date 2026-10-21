<#
  Loads a starter Montessori curriculum and lesson plan templates per age group into a LOCAL development system through the API.
  Run load-learning-data.ps1 first for the EYFS templates. Nothing ships with the app; drop the NMS database to remove it.

  Usage:  powershell -File tools\load-montessori-data.ps1
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

# 1. Montessori curriculum (Children's House): 5 areas, 4 age bands, 20 milestones in English and Arabic.
$mont = @(Call 'GET' '/learning/frameworks') | Where-Object { $_.code -eq 'montessori' } | Select-Object -First 1
if (-not $mont) { $mont = Call 'POST' '/learning/frameworks' @{ code = 'montessori'; name = @{ en = 'Montessori (Children''s House)'; ar = 'مونتيسوري (بيت الأطفال)' } } }
$json = [System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'data\montessori-starter.json'))
$r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Api/api/v1/learning/frameworks/$($mont.id)/import" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $json
$mt = ($r.Content | ConvertFrom-Json).data
Write-Host "Montessori ready: $($mt.areas.Count) areas, $($mt.age_bands.Count) age bands, $($mt.milestones.Count) milestones."

# 2. Lesson plan templates per age group (EYFS and Montessori).
$eyfs = @(Call 'GET' '/learning/frameworks') | Where-Object { $_.code -eq 'eyfs' } | Select-Object -First 1
$made = 0
$templates = @()
if ($eyfs) {
    $et = Call 'GET' "/learning/frameworks/$($eyfs.id)"
    $ea = @{}; $et.areas | ForEach-Object { $ea[$_.code] = $_.id }; $eb = @{}; $et.age_bands | ForEach-Object { $eb[$_.code] = $_.id }
    $templates += @{ fw = $eyfs.id; area = $ea['cal']; band = $eb['b8-20']; en = 'Infants: songs, peek-a-boo and sensory baskets'; ar = 'الرضع: أغاني واختباء ومحاضن حسية'; min = 20; mat = @('Soft scarves','Treasure basket','Mirror'); obj = @('Respond to their name','Explore textures') }
    $templates += @{ fw = $eyfs.id; area = $ea['pd']; band = $eb['b22-36']; en = 'Toddlers: move, climb and balance circuit'; ar = 'الأطفال الصغار: مسار الحركة والتسلق والتوازن'; min = 30; mat = @('Soft blocks','Low steps','Hoops'); obj = @('Walk, run and climb safely','Balance briefly') }
    $templates += @{ fw = $eyfs.id; area = $ea['l']; band = $eb['b40-60']; en = 'Preschool: letter sounds and first writing'; ar = 'ما قبل المدرسة: أصوات الحروف وأولى الكتابة'; min = 40; mat = @('Letter cards','Sand tray','Crayons'); obj = @('Hear the first sound of a word','Write the letters of their name') }
}
if ($mt.areas.Count -gt 0) {
    $ma = @{}; $mt.areas | ForEach-Object { $ma[$_.code] = $_.id }; $mb = @{}; $mt.age_bands | ForEach-Object { $mb[$_.code] = $_.id }
    $templates += @{ fw = $mont.id; area = $ma['practical-life']; band = $mb['c36-48']; en = 'Children''s House: pouring and food preparation'; ar = 'بيت الأطفال: الصب وتحضير الطعام'; min = 35; mat = @('Small jugs','Trays','Sponges','Fruit and a safe knife'); obj = @('Pour without spilling','Prepare a snack'); }
    $templates += @{ fw = $mont.id; area = $ma['mathematics']; band = $mb['c48-60']; en = 'Children''s House: golden beads and the decimal system'; ar = 'بيت الأطفال: الخرز الذهبي والنظام العشري'; min = 40; mat = @('Golden bead set','Number cards'); obj = @('Build numbers to a thousand') }
}
foreach ($t in $templates) {
    $body = @{ title = @{ en = $t.en; ar = $t.ar }; framework_id = $t.fw; area_id = $t.area; age_band_id = $t.band; duration_minutes = $t.min; materials = $t.mat; objectives = $t.obj; is_template = $true }
    if (Try-Call 'POST' '/learning/lessons' $body) { $made++ }
}
Write-Host "Lesson plan templates added: $made."
