<#
  Adds a large batch of test children (default 100) spread over the classes by age, more staff, today's attendance and learning records
  (observations, assessments, lessons) to a LOCAL development system through the API. Nothing ships with the app; drop the NMS database to remove it.
  For speed run it against the copy with the request limits lifted: powershell -File tools\start-load-copy.ps1, then -Api http://localhost:5191.

  Usage:  powershell -File tools\load-bulk-data.ps1 [-Api http://localhost:5178] [-Students 100] [-ExtraStaff 12]
#>
param([string]$Api = 'http://localhost:5178', [int]$Students = 100, [int]$ExtraStaff = 12)
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
        $detail = if ($failure.ErrorDetails) { $failure.ErrorDetails.Message } elseif ($failure.Exception.Response) { try { (New-Object System.IO.StreamReader($failure.Exception.Response.GetResponseStream())).ReadToEnd() } catch { $failure.Exception.Message } } else { $failure.Exception.Message }
        throw "$method $path failed: $detail"
    }
    ($r.Content | ConvertFrom-Json).data
}
function Try-Call($method, $path, $body = $null) { try { Call $method $path $body } catch { Write-Host "  (skipped: $($_.Exception.Message.Substring(0, [Math]::Min(220, $_.Exception.Message.Length))))" -ForegroundColor DarkYellow; $null } }

# Sign in through the local-only test-user helper.
$login = Call 'POST' '/dev/quick-login' @{ role = 'it' }
$headers['Authorization'] = "Bearer $($login.access_token)"
Write-Host 'Signed in as the local test IT administrator.'
$rng = New-Object System.Random
function Pick($list) { $list[$rng.Next(0, $list.Count)] }
$stamp = (Get-Date).ToString('MMddHHmm')

# ── Name pools (English and Arabic) ──
$girls = @(@('Fatima','فاطمة'),@('Maryam','مريم'),@('Aisha','عائشة'),@('Noura','نورة'),@('Hessa','حصة'),@('Latifa','لطيفة'),@('Shamma','شما'),@('Mouza','موزة'),@('Salama','سلامة'),@('Reem','ريم'),@('Layla','ليلى'),@('Sara','سارة'),@('Dana','دانة'),@('Alia','عالية'),@('Amna','آمنة'),@('Maitha','ميثاء'),@('Khawla','خولة'),@('Zainab','زينب'),@('Lina','لينا'),@('Yasmin','ياسمين'))
$boys = @(@('Mohammed','محمد'),@('Ahmed','أحمد'),@('Khalid','خالد'),@('Rashid','راشد'),@('Hamdan','حمدان'),@('Zayed','زايد'),@('Sultan','سلطان'),@('Saeed','سعيد'),@('Omar','عمر'),@('Ali','علي'),@('Yousef','يوسف'),@('Ibrahim','إبراهيم'),@('Hamad','حمد'),@('Mansour','منصور'),@('Salem','سالم'),@('Faisal','فيصل'),@('Adam','آدم'),@('Karim','كريم'),@('Nasser','ناصر'),@('Majid','ماجد'))
$families = @(@('Al Mansoori','المنصوري'),@('Al Ketbi','الكتبي'),@('Al Nuaimi','النعيمي'),@('Al Shamsi','الشامسي'),@('Al Marri','المري'),@('Al Falasi','الفلاسي'),@('Al Hammadi','الحمادي'),@('Al Blooshi','البلوشي'),@('Al Dhaheri','الظاهري'),@('Al Suwaidi','السويدي'),@('Al Mazrouei','المزروعي'),@('Al Zaabi','الزعابي'),@('Al Hashemi','الهاشمي'),@('Khan','خان'),@('Haddad','حداد'),@('Thompson','طومسون'),@('Patel','باتيل'),@('Hassan','حسن'),@('Rahman','رحمن'),@('Nasser','ناصر'),@('Farouk','فاروق'),@('Sharma','شارما'),@('Mirza','ميرزا'),@('Martin','مارتن'),@('Johnson','جونسون'))

# ── Classes (rooms): keep the existing ones and add four more so about 100 children are spread out ──
$roomList = @(Call 'GET' '/rooms?limit=100')
$wanted = @(
    @('Rainbow Room','غرفة قوس قزح',25,12,24,5), @('Ocean Room','غرفة المحيط',25,24,36,6),
    @('Forest Room','غرفة الغابة',25,36,48,7), @('Moon Room','غرفة القمر',25,48,60,8))
foreach ($w in $wanted) {
    if (-not ($roomList | Where-Object { $_.name.en -eq $w[0] })) {
        Try-Call 'POST' '/rooms' @{ name = @{ en = $w[0]; ar = $w[1] }; capacity = $w[2]; min_age_months = $w[3]; max_age_months = $w[4]; ratio_staff_to_student = $w[5] } | Out-Null
    }
}
$roomList = @(Call 'GET' '/rooms?limit=100') | Where-Object { $_.is_active -ne $false }
$programs = @(Call 'GET' '/programs?limit=100')
Write-Host "Classes available: $($roomList.Count)."

# ── Staff ──
$staffPool = @(
    @('Amal','أمل','Al Hosani','الحوسني','Lead teacher'), @('Shaikha','شيخة','Al Qubaisi','القبيسي','Lead teacher'), @('Rania','رانيا','Haddad','حداد','Lead teacher'), @('Priya','بريا','Nair','ناير','Lead teacher'),
    @('Mona','منى','Al Tamimi','التميمي','Teacher'), @('Sana','سناء','Iqbal','إقبال','Teacher'), @('Huda','هدى','Al Ali','العلي','Teacher'), @('Jane','جين','Walker','ووكر','Teacher'),
    @('Ruqaya','رقية','Al Jaberi','الجابري','Teaching assistant'), @('Dalal','دلال','Mahmoud','محمود','Teaching assistant'), @('Eman','إيمان','Saleh','صالح','Teaching assistant'), @('Joy','جوي','Santos','سانتوس','Nursery nurse'),
    @('Khadija','خديجة','Omar','عمر','Cook'), @('Lubna','لبنى','Yousef','يوسف','Nursery nurse'), @('Wafa','وفاء','Ahmed','أحمد','Teaching assistant'), @('Nadia','نادية','Rashid','راشد','Teacher')
)
$roomsForStaff = @($roomList)
$staffMade = 0
$si = 3
foreach ($p in ($staffPool | Select-Object -First $ExtraStaff)) {
    $mail = "$($p[0].ToLower()).$($p[2].ToLower().Replace(' ', '')).$stamp@demo.local"
    $created = Try-Call 'POST' '/staff' @{ staff = @{ first_name = $p[0]; last_name = $p[2]; first_name_arabic = $p[1]; last_name_arabic = $p[3]; email = $mail; phone = '+97150' + $rng.Next(1000000, 9999999); hire_date = (Get-Date).AddMonths(-$rng.Next(2, 36)).ToString('yyyy-MM-dd'); status = 'active'; job_title = $p[4]; salary_amount_fils = $rng.Next(380, 900) * 1000 } }
    if (-not $created) { continue }
    $staffMade++
    if ($p[4] -in @('Lead teacher','Teacher','Teaching assistant')) {
        $room = $roomsForStaff[$si % $roomsForStaff.Count]; $si++
        foreach ($day in 0..4) {
            Try-Call 'POST' "/staff/$($created.id)/room-assignments" @{ room_id = $room.id; day_of_week = $day; start_time = '07:30:00'; end_time = '15:30:00'; effective_from = (Get-Date).AddDays(-30).ToString('yyyy-MM-dd') } | Out-Null
        }
        if ($rng.Next(0, 100) -lt 85) { $null = Try-Call 'POST' '/attendance/actions/check-in' @{ person_type = 'staff'; person_id = $created.id; room_id = $room.id } }
    }
}
Write-Host "Staff added: $staffMade."

# ── Children, spread over the classes by age ──
$counts = @{}; $roomList | ForEach-Object { $counts[$_.id] = 0 }
$children = New-Object System.Collections.ArrayList
for ($n = 1; $n -le $Students; $n++) {
    $room = $roomList[($n + $stamp.Length) % $roomList.Count]
    $lo = [Math]::Max(6, [int]$room.min_age_months); $hi = [Math]::Min(59, [int]$room.max_age_months)
    if ($hi -le $lo) { $hi = $lo + 6 }
    $age = $rng.Next($lo, $hi + 1)
    $counts[$room.id]++
    $progFit = @($programs | Where-Object { $age -ge $_.min_age_months -and $age -le $_.max_age_months })
    $girl = ($n % 2 -eq 0)
    $first = if ($girl) { Pick $girls } else { Pick $boys }
    $fam = Pick $families
    $mom = Pick $girls
    $allergy = if ($n % 9 -eq 0) { 'Peanuts' } elseif ($n % 13 -eq 0) { 'Dairy' } else { $null }
    $momMail = "$($mom[0].ToLower()).$($fam[0].ToLower().Replace(' ', '')).$stamp.$n@demo.local"
    $made = Try-Call 'POST' '/students' @{
        student = @{ first_name = $first[0]; last_name = $fam[0]; first_name_arabic = $first[1]; last_name_arabic = $fam[1]; gender = $(if ($girl) { 'female' } else { 'male' })
            date_of_birth = (Get-Date).AddMonths(-$age).AddDays(-$rng.Next(0, 25)).ToString('yyyy-MM-dd'); room_id = $room.id; program_id = $(if ($progFit.Count -gt 0) { $progFit[$n % $progFit.Count].id } else { $null })
            allergy_info = $allergy; medical_notes = $(if ($allergy) { "Carries an action plan for $allergy" } else { $null }); nationality = 'AE'; primary_language = $(if ($n % 3 -eq 0) { 'ar' } else { 'en' }) }
        guardians = @(@{ first_name = $mom[0]; last_name = $fam[0]; email = $momMail; phone = '+97155' + $rng.Next(1000000, 9999999); relationship = 'mother'; is_primary = $true; can_pickup = $true })
    }
    if (-not $made) { continue }
    $null = Try-Call 'POST' "/students/$($made.id)/actions/transition-status" @{ status = 'active' }
    $null = $children.Add(@{ id = $made.id; age = $age; room = $room.id; name = "$($first[0]) $($fam[0])" })
}
Write-Host "Children added: $($children.Count). Per class:"
$roomList | ForEach-Object { Write-Host ("  {0,-18} {1}" -f $_.name.en, $counts[$_.id]) }

# ── Today's attendance for about 70 percent of the new children ──
$in = 0
foreach ($c in ($children | Where-Object { $rng.Next(0, 100) -lt 70 })) {
    if (Try-Call 'POST' '/attendance/actions/check-in' @{ person_type = 'student'; person_id = $c.id; room_id = $c.room }) { $in++ }
}
Write-Host "Checked in today: $in."

# ── Learning: observations for everyone, assessments for many, lessons for the new classes ──
$eyfs = @(Call 'GET' '/learning/frameworks') | Where-Object { $_.code -eq 'eyfs' } | Select-Object -First 1
if (-not $eyfs) { Write-Host 'No EYFS curriculum found; run tools\load-learning-data.ps1 first. Learning data skipped.'; return }
$tree = Call 'GET' "/learning/frameworks/$($eyfs.id)"
$areaIds = @($tree.areas | ForEach-Object { $_.id })
$bands = @($tree.age_bands)
function BandFor($months) { ($bands | Where-Object { $months -ge $_.min_months -and $months -le $_.max_months } | Select-Object -Last 1) }
$phrases = @(
    @('informal', 'Played happily with a friend and shared the toys.', 'لعب بسعادة مع صديق وشارك الألعاب.'),
    @('informal', 'Listened to the whole story and answered the questions.', 'استمع إلى القصة كاملة وأجاب عن الأسئلة.'),
    @('informal', 'Tried a new food at lunch and said it was yummy.', 'جرب طعاما جديدا في الغداء وقال إنه لذيذ.'),
    @('formal', 'Built a tower of eight blocks and counted them aloud.', 'بنى برجا من ثماني مكعبات وعدها بصوت عال.'),
    @('informal', 'Sang the Arabic rhyme with the group at circle time.', 'غنى النشيد العربي مع المجموعة في وقت الحلقة.'),
    @('informal', 'Used the paintbrush with a steady grip and named every colour.', 'استخدم الفرشاة بقبضة ثابتة وسمى كل الألوان.'),
    @('formal', 'Jumped with both feet and balanced on one foot for three seconds.', 'قفز بكلتا قدميه وتوازن على قدم واحدة لثلاث ثوان.'),
    @('informal', 'Helped tidy the room without being asked.', 'ساعد في ترتيب الغرفة دون أن يطلب منه.'),
    @('informal', 'Watched the ants in the garden and asked where they live.', 'راقب النمل في الحديقة وسأل أين يعيش.')
)
$obsMade = 0
foreach ($c in $children) {
    $band = BandFor $c.age
    $count = if ($rng.Next(0, 100) -lt 35) { 2 } else { 1 }
    for ($k = 0; $k -lt $count; $k++) {
        $ph = Pick $phrases
        $type = $ph[0]
        if ($rng.Next(0, 100) -lt 4) { $type = 'concern' }
        $body = @{ student_id = $c.id; type = $type; framework_id = $eyfs.id; area_id = (Pick $areaIds); share_with_parents = ($type -ne 'concern' -and $rng.Next(0, 100) -lt 70); notes = @{ en = $ph[1]; ar = $ph[2] } }
        if ($band) { $body.age_band_id = $band.id }
        if (Try-Call 'POST' '/learning/observations' $body) { $obsMade++ }
    }
}
Write-Host "Observations added: $obsMade."

$templates = @(Call 'GET' '/learning/assessment-templates')
$runs = 0; $approved = 0
if ($templates.Count -gt 0) {
    foreach ($c in ($children | Where-Object { $rng.Next(0, 100) -lt 45 })) {
        $tpl = if ($c.age -ge 36 -and $templates.Count -gt 1) { $templates[1] } else { $templates[0] }
        $run = Try-Call 'POST' '/learning/assessments' @{ template_id = $tpl.id; student_id = $c.id; term = "Term-$stamp" }
        if (-not $run) { continue }
        $runs++
        $max = if ($run.scoring_kind -eq 'binary') { 1 } else { 2 }
        $scores = @($run.scores | ForEach-Object { @{ milestone_id = $_.milestone_id; score = $rng.Next(0, $max + 1); note = '' } })
        $summary = @{ en = "$($c.name) is settling in well, enjoys playing with friends and is making steady progress."; ar = 'يتأقلم جيدا ويستمتع باللعب مع الأصدقاء ويحقق تقدما ثابتا.' }
        $null = Try-Call 'PUT' "/learning/assessments/$($run.id)/scores" @{ scores = $scores; staff_summary = @{ en = 'Progress in line with age band.' }; parent_summary = $summary }
        $null = Try-Call 'POST' "/learning/assessments/$($run.id)/actions/complete"
        if ($rng.Next(0, 100) -lt 60) { if (Try-Call 'POST' "/learning/assessments/$($run.id)/actions/approve") { $approved++ } }
    }
}
Write-Host "Assessments started: $runs, approved for families: $approved."

$lessonSets = @(
    @('Colour sorting baskets','فرز الألوان في السلال','ead'), @('Songs about the days of the week','أغاني أيام الأسبوع','cal'), @('Splash and pour','رش وصب الماء','pd'),
    @('Shape detectives','محققو الأشكال','m'), @('Our community helpers','أبطال مجتمعنا','uw'), @('Kindness circle','حلقة اللطف','psed'), @('Letter sounds treasure hunt','البحث عن أصوات الحروف','l'), @('Mud kitchen','مطبخ الطين','uw')
)
$area = @{}; $tree.areas | ForEach-Object { $area[$_.code] = $_.id }
$li = 0; $lessonsMade = 0
foreach ($r in $roomsForStaff) {
    foreach ($k in 0..1) {
        $ls = $lessonSets[$li % $lessonSets.Count]; $li++
        $b = BandFor ([int](($r.min_age_months + $r.max_age_months) / 2))
        $body = @{ title = @{ en = $ls[0]; ar = $ls[1] }; framework_id = $eyfs.id; area_id = $area[$ls[2]]; room_id = $r.id; duration_minutes = 30 + 5 * $k; materials = @('Everyday classroom materials'); objectives = @('Join in and have fun', 'Use new words') }
        if ($b) { $body.age_band_id = $b.id }
        if (Try-Call 'POST' '/learning/lessons' $body) { $lessonsMade++ }
    }
}
Write-Host "Lessons added: $lessonsMade."
Write-Host "Done: $($children.Count) children, $staffMade staff, $obsMade observations, $runs assessments, $lessonsMade lessons."
