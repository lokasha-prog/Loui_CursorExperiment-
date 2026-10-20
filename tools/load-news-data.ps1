<#
  Adds test newsletters and library documents to a LOCAL development system through the API, as if made on screen:
  published, scheduled and draft newsletters in English and Arabic, library folders for parents and staff, and documents with extra versions.
  Nothing ships with the app; drop the NMS database to remove it all.

  Usage:  powershell -File tools\load-news-data.ps1
#>
Add-Type -AssemblyName System.Net.Http
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
function Upload($path, $fields, $fileName, [byte[]]$bytes) {
    $client = New-Object System.Net.Http.HttpClient
    $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $login.access_token)
    $form = New-Object System.Net.Http.MultipartFormDataContent
    foreach ($k in $fields.Keys) { $form.Add((New-Object System.Net.Http.StringContent([string]$fields[$k])), $k) }
    $part = New-Object System.Net.Http.ByteArrayContent(, $bytes)
    $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
    $form.Add($part, 'file', $fileName)
    $response = $client.PostAsync("$Api/api/v1$path", $form).Result
    $text = $response.Content.ReadAsStringAsync().Result
    if (-not $response.IsSuccessStatusCode) { Write-Host "  (skipped upload: $text)" -ForegroundColor DarkYellow; return $null }
    ($text | ConvertFrom-Json).data
}
function Pdf($text) { [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n1 0 obj<</Type/Catalog>>endobj`n% $text`ntrailer<</Root 1 0 R>>`n%%EOF") }

$rooms = @(Call 'GET' '/rooms?limit=100')
$news = @(
  @('Welcome back to term two', 'مرحباً بكم في الفصل الثاني', '<h2>Welcome back</h2><p>We are so happy to see everyone. Please read the <a href="https://example.com/term-dates">term dates</a>.</p>', '<h2>أهلاً بعودتكم</h2><p>يسعدنا رؤية الجميع.</p>', 'publish'),
  @('Healthy snack week', 'أسبوع الوجبات الصحية', '<p>Next week we explore fruit and vegetables. <strong>No nuts please.</strong></p>', '<p>الأسبوع القادم نستكشف الفواكه والخضروات. <strong>بدون مكسرات من فضلكم.</strong></p>', 'publish'),
  @('Parent evening invitation', 'دعوة لأمسية أولياء الأمور', '<p>Join us for a short evening with the teachers.</p><ul><li>Meet the teachers</li><li>See your child''s room</li></ul>', '<p>انضموا إلينا لأمسية قصيرة مع المعلمات.</p>', 'publish'),
  @('Winter clothing reminder', 'تذكير بملابس الشتاء', '<p>Please send a jumper on cooler mornings.</p>', '<p>يرجى إرسال سترة في الصباحات الباردة.</p>', 'schedule'),
  @('Summer programme preview', 'لمحة عن البرنامج الصيفي', '<p>Our summer programme is taking shape. Details soon.</p>', '<p>برنامجنا الصيفي قيد الإعداد. التفاصيل قريباً.</p>', 'draft'),
  @('Art exhibition', 'معرض الفنون', '<h2>Little artists</h2><p>Come and see the children''s paintings.</p>', '<h2>فنانون صغار</h2><p>تعالوا لمشاهدة لوحات الأطفال.</p>', 'draft')
)
$made = 0
foreach ($n in $news) {
    $audience = if ($rooms.Count -gt 0 -and $made % 3 -eq 2) { @{ audience_type = 'room'; audience_ids = @($rooms[0].id) } } else { @{ audience_type = 'all'; audience_ids = @() } }
    $r = Try-Call 'POST' '/newsletters' (@{ title = @{ en = $n[0]; ar = $n[1] }; body = @{ en = $n[2]; ar = $n[3] } } + $audience)
    if (-not $r) { continue }
    $id = $r.newsletter.id
    if ($n[4] -eq 'publish') { Try-Call 'POST' "/newsletters/$id/actions/publish" @{} | Out-Null }
    if ($n[4] -eq 'schedule') { Try-Call 'POST' "/newsletters/$id/actions/schedule" @{ at = (Get-Date).AddDays(4).ToUniversalTime().ToString('o') } | Out-Null }
    $made++
}

# Library: two folders for parents, two for staff, documents in each, and a second version of a few.
$folderIds = @{}
foreach ($f in @(@('Term dates and fees', 'مواعيد الفصول والرسوم', 'parents'), @('Health and safety', 'الصحة والسلامة', 'parents'), @('Staff handbook', 'دليل الموظفين', 'staff'), @('Training materials', 'مواد التدريب', 'staff'))) {
    $list = Try-Call 'POST' '/resources/folders' @{ name = @{ en = $f[0]; ar = $f[1] }; audience = $f[2] }
    if ($list) { $folderIds[$f[0]] = ($list | Where-Object { $_.name.en -eq $f[0] } | Select-Object -First 1).id }
}
$docs = @(
  @('Term dates and fees', 'Term dates 2026-27', 'terms, dates'), @('Term dates and fees', 'Fee schedule', 'fees, payment'), @('Health and safety', 'Illness policy', 'health, illness'),
  @('Health and safety', 'Allergy action plan', 'allergy, health'), @('Staff handbook', 'Code of conduct', 'staff, conduct'), @('Staff handbook', 'Leave policy', 'staff, leave'),
  @('Training materials', 'Fire drill procedure', 'safety, training'), @('Training materials', 'First aid guide', 'first aid, training')
)
$uploaded = @()
foreach ($d in $docs) {
    if (-not $folderIds[$d[0]]) { continue }
    $doc = Upload '/resources/documents' @{ folder_id = $folderIds[$d[0]]; title = $d[1]; tags = $d[2]; description = 'Test document' } "$($d[1] -replace ' ', '-').pdf" (Pdf $d[1])
    if ($doc) { $uploaded += $doc }
}
foreach ($d in $uploaded | Select-Object -First 3) { Upload "/resources/documents/$($d.id)/versions" @{} "$($d.title -replace ' ', '-')-v2.pdf" (Pdf "$($d.title) version 2") | Out-Null }
Write-Host "Done: $made newsletters and $($uploaded.Count) documents (3 with a second version) loaded." -ForegroundColor Green
