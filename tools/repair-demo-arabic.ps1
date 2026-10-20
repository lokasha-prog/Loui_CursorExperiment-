# One-off repair for demo data loaded before the loader sent real UTF-8: puts the Arabic names back through the API.
# Safe to run more than once. Development only.
param([string]$Api = 'http://localhost:5178')
$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$headers = @{}
function Call($method, $path, $body = $null) {
    $a = @{ Uri = "$Api/api/v1$path"; Method = $method; WebSession = $session; Headers = $headers; ContentType = 'application/json; charset=utf-8'; UseBasicParsing = $true }
    if ($null -ne $body) { $a.Body = [System.Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8 -Compress)) }
    for ($i = 0; $i -lt 6; $i++) {
        try { return ((Invoke-WebRequest @a).Content | ConvertFrom-Json).data } catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 429) { Start-Sleep 15; continue }
            throw
        }
    }
}
$login = Call 'POST' '/dev/quick-login' @{ role = 'it' }
$headers['Authorization'] = "Bearer $($login.access_token)"

$roomAr = @{ 'Little Stars' = 'النجوم الصغيرة'; 'Busy Bees' = 'النحلات النشيطة'; 'Sunshine' = 'أشعة الشمس' }
foreach ($r in (Call 'GET' '/rooms?limit=100')) {
    $en = $r.name.en
    if ($roomAr.ContainsKey($en)) {
        Call 'PATCH' "/rooms/$($r.id)" @{ name = @{ en = $en; ar = $roomAr[$en] }; capacity = $r.capacity; min_age_months = $r.min_age_months; max_age_months = $r.max_age_months; ratio_staff_to_student = $r.ratio_staff_to_student; is_active = $r.is_active } | Out-Null
    }
}
$programAr = @{ 'Full day' = @('يوم كامل', 12, 60); 'Morning only' = @('صباحي فقط', 12, 36) }
foreach ($p in (Call 'GET' '/programs?limit=100')) {
    $en = $p.name.en
    if ($programAr.ContainsKey($en)) {
        $v = $programAr[$en]
        Call 'PATCH' "/programs/$($p.id)" @{ name = @{ en = $en; ar = $v[0] }; description = $null; days_per_week = $p.days_per_week; start_time = $p.start_time; end_time = $p.end_time; age_group = $p.age_group; min_age_months = $v[1]; max_age_months = $v[2]; is_active = $p.is_active } | Out-Null
    }
}
$kids = @{
    'Layla' = @('ليلى', 'السويدي'); 'Omar' = @('عمر', 'حداد'); 'Yusuf' = @('يوسف', 'خان'); 'Maya' = @('مايا', 'طومسون'); 'Zayed' = @('زايد', 'النعيمي'); 'Amira' = @('أميرة', 'فاروق')
    'Ali' = @('علي', 'ميرزا'); 'Noura' = @('نورة', 'الفلاسي'); 'Hamdan' = @('حمدان', 'الشامسي'); 'Sophie' = @('صوفي', 'مارتن'); 'Rashid' = @('راشد', 'المري'); 'Dana' = @('دانة', 'ناصر')
}
$done = 0
foreach ($s in (Call 'GET' '/students?limit=100')) {
    if ($kids.ContainsKey($s.first_name) -and $s.last_name -notin @('Okasha')) {
        $d = Call 'GET' "/students/$($s.id)"
        if ($d.first_name_arabic -eq $kids[$s.first_name][0]) { continue }
        $f = @{
            first_name = $d.first_name; last_name = $d.last_name; first_name_arabic = $kids[$s.first_name][0]; last_name_arabic = $kids[$s.first_name][1]
            date_of_birth = $d.date_of_birth; gender = $d.gender; room_id = $d.room_id; group_id = $d.group_id; program_id = $d.program_id; key_person_staff_id = $d.key_person_staff_id
            start_date = $d.start_date; nationality = $d.nationality; religion = $d.religion; primary_language = $d.primary_language; medical_notes = $d.medical_notes
            allergy_info = $d.allergy_info; dietary_requirements = $d.dietary_requirements; custody_notes = $d.custody_notes; passport_number = $d.passport_number
            emirates_id = $d.emirates_id; label_ids = @($d.label_ids); photo_media_id = $d.photo_media_id; id_photo_media_id = $d.id_photo_media_id
        }
        Call 'PATCH' "/students/$($s.id)" @{ student = $f } | Out-Null
        $done++
    }
}
Write-Host "Repaired Arabic text for rooms, programmes and $done children."
