<#
  Fills a LOCAL development system with realistic test data, by calling the same API the screens use (as if a person typed it).

  - Nothing here is part of the application or its database migrations: the app ships empty.
  - It works only against a development server on this computer (it signs in with the local test-user helper).
  - To remove everything, drop the NMS database and start the API again (migrations recreate an empty one).
  - Passwords for the accounts it creates are random and thrown away; use the test-user buttons on the login screen.

  Usage:  powershell -File tools\load-demo-data.ps1            (add -Force to add a second batch)
#>
param([string]$Api = 'http://localhost:5178', [switch]$Force)
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

$existing = Call 'GET' '/rooms?limit=100'
if (($existing | Where-Object { $_.name.en -like 'Little Stars*' }) -and -not $Force) { throw 'Demo data already loaded (use -Force to add another batch).' }
$tag = if ($Force) { " $(Get-Random -Maximum 99)" } else { '' }

# ── Centre structure ──
$rooms = @{}
foreach ($r in @(
    @{ k = 'stars'; en = 'Little Stars'; ar = 'النجوم الصغيرة'; cap = 10; min = 12; max = 24; ratio = 4 },
    @{ k = 'bees'; en = 'Busy Bees'; ar = 'النحلات النشيطة'; cap = 14; min = 24; max = 36; ratio = 6 },
    @{ k = 'sun'; en = 'Sunshine'; ar = 'أشعة الشمس'; cap = 16; min = 36; max = 60; ratio = 8 })) {
    $rooms[$r.k] = (Call 'POST' '/rooms' @{ name = @{ en = "$($r.en)$tag"; ar = $r.ar }; capacity = $r.cap; min_age_months = $r.min; max_age_months = $r.max; ratio_staff_to_student = $r.ratio }).id
}
$programs = @()
foreach ($p in @(@{ en = 'Full day'; ar = 'يوم كامل'; s = '07:30:00'; e = '15:30:00'; d = 5; min = 12; max = 60 }, @{ en = 'Morning only'; ar = 'صباحي فقط'; s = '07:30:00'; e = '12:00:00'; d = 5; min = 12; max = 36 })) {
    $programs += (Call 'POST' '/programs' @{ name = @{ en = "$($p.en)$tag"; ar = $p.ar }; description = $null; days_per_week = $p.d; start_time = $p.s; end_time = $p.e; age_group = '1-4 years'; min_age_months = $p.min; max_age_months = $p.max; is_active = $true }).id
}
Write-Host "Created $($rooms.Count) rooms and $($programs.Count) programmes."

# ── Staff ──
$staffPeople = @(
    @{ f = 'Noor'; l = 'Al Mansoori'; t = 'Lead teacher'; room = 'stars'; user = 'teacher' },
    @{ f = 'Hessa'; l = 'Al Ketbi'; t = 'Teacher'; room = 'stars'; user = $null },
    @{ f = 'Mariam'; l = 'Khan'; t = 'Lead teacher'; room = 'bees'; user = $null },
    @{ f = 'Aisha'; l = 'Rahman'; t = 'Teaching assistant'; room = 'bees'; user = $null },
    @{ f = 'Fatima'; l = 'Haddad'; t = 'Receptionist'; room = $null; user = 'receptionist' })
$staff = @()
foreach ($s in $staffPeople) {
    $mail = "$($s.f.ToLower()).$($s.l.ToLower().Replace(' ', ''))$($tag.Trim())@demo.local"
    $created = Call 'POST' '/staff' @{ staff = @{ first_name = $s.f; last_name = $s.l; email = $mail; phone = '+97150' + (Get-Random -Minimum 1000000 -Maximum 9999999); hire_date = (Get-Date).AddMonths(-(Get-Random -Minimum 3 -Maximum 30)).ToString('yyyy-MM-dd'); status = 'active'; job_title = $s.t; salary_amount_fils = (Get-Random -Minimum 400 -Maximum 900) * 1000 } }
    $staff += @{ id = $created.id; room = $s.room; user = $s.user; name = "$($s.f) $($s.l)" }
    if ($s.room) {
        foreach ($day in 0..4) {
            Try-Call 'POST' "/staff/$($created.id)/room-assignments" @{ room_id = $rooms[$s.room]; day_of_week = $day; start_time = '07:30:00'; end_time = '15:30:00'; effective_from = (Get-Date).AddDays(-30).ToString('yyyy-MM-dd') } | Out-Null
        }
    }
}
Write-Host "Created $($staff.Count) staff members."

# ── Children and families ──
$families = @(
    @{ c = 'Layla'; ca = 'ليلى'; l = 'Al Suwaidi'; la = 'السويدي'; g = 'female'; age = 20; room = 'stars'; mom = 'Sara'; user = 'parent' },
    @{ c = 'Omar'; ca = 'عمر'; l = 'Haddad'; la = 'حداد'; g = 'male'; age = 18; room = 'stars'; mom = 'Rania'; user = $null },
    @{ c = 'Yusuf'; ca = 'يوسف'; l = 'Khan'; la = 'خان'; g = 'male'; age = 15; room = 'stars'; mom = 'Zainab'; user = $null },
    @{ c = 'Maya'; ca = 'مايا'; l = 'Thompson'; la = 'طومسون'; g = 'female'; age = 22; room = 'stars'; mom = 'Emma'; user = $null },
    @{ c = 'Zayed'; ca = 'زايد'; l = 'Al Nuaimi'; la = 'النعيمي'; g = 'male'; age = 30; room = 'bees'; mom = 'Mouza'; user = $null },
    @{ c = 'Amira'; ca = 'أميرة'; l = 'Farouk'; la = 'فاروق'; g = 'female'; age = 28; room = 'bees'; mom = 'Dina'; user = $null },
    @{ c = 'Ali'; ca = 'علي'; l = 'Mirza'; la = 'ميرزا'; g = 'male'; age = 33; room = 'bees'; mom = 'Hina'; user = $null },
    @{ c = 'Noura'; ca = 'نورة'; l = 'Al Falasi'; la = 'الفلاسي'; g = 'female'; age = 26; room = 'bees'; mom = 'Shamsa'; user = $null },
    @{ c = 'Hamdan'; ca = 'حمدان'; l = 'Al Shamsi'; la = 'الشامسي'; g = 'male'; age = 44; room = 'sun'; mom = 'Latifa'; user = $null },
    @{ c = 'Sophie'; ca = 'صوفي'; l = 'Martin'; la = 'مارتن'; g = 'female'; age = 40; room = 'sun'; mom = 'Claire'; user = $null },
    @{ c = 'Rashid'; ca = 'راشد'; l = 'Al Marri'; la = 'المري'; g = 'male'; age = 48; room = 'sun'; mom = 'Salama'; user = $null },
    @{ c = 'Dana'; ca = 'دانة'; l = 'Nasser'; la = 'ناصر'; g = 'female'; age = 38; room = 'sun'; mom = 'Huda'; user = $null })
$children = @()
$i = 0
foreach ($f in $families) {
    $i++
    $allergy = if ($i % 4 -eq 0) { 'Peanuts' } elseif ($i % 5 -eq 0) { 'Dairy' } else { $null }
    $momMail = "$($f.mom.ToLower()).$($f.l.ToLower().Replace(' ', ''))$($tag.Trim())@demo.local"
    $made = Call 'POST' '/students' @{
        student = @{ first_name = $f.c; last_name = $f.l; first_name_arabic = $f.ca; last_name_arabic = $f.la; gender = $f.g
            date_of_birth = (Get-Date).AddMonths(-$f.age).AddDays(-(Get-Random -Maximum 25)).ToString('yyyy-MM-dd'); room_id = $rooms[$f.room]; program_id = $programs[$i % 2]
            allergy_info = $allergy; medical_notes = $(if ($allergy) { "Carries an action plan for $allergy" } else { $null }); nationality = 'AE'; emirates_id = '784-20' + (Get-Random -Minimum 20 -Maximum 26) + '-' + (Get-Random -Minimum 1000000 -Maximum 9999999) + '-1'; primary_language = $(if ($i % 3 -eq 0) { 'ar' } else { 'en' }) }
        guardians = @(
            @{ first_name = $f.mom; last_name = $f.l; email = $momMail; phone = '+97155' + (Get-Random -Minimum 1000000 -Maximum 9999999); relationship = 'mother'; is_primary = $true; can_pickup = $true; emirates_id = '784-19' + (Get-Random -Minimum 70 -Maximum 99) + '-' + (Get-Random -Minimum 1000000 -Maximum 9999999) + '-2'; receives_communications = $true },
            @{ first_name = 'Grandparent'; last_name = $f.l; email = "grand.$momMail"; relationship = 'grandparent'; is_primary = $false; can_pickup = ($i % 2 -eq 0); receives_communications = $false })
    }
    Call 'POST' "/students/$($made.id)/actions/transition-status" @{ status = 'active' } | Out-Null
    $detail = Call 'GET' "/students/$($made.id)"
    $guardians = Call 'GET' "/students/$($made.id)/guardians"
    $children += @{ id = $made.id; barcode = $detail.barcode; room = $f.room; name = "$($f.c) $($f.l)"; mother = ($guardians | Where-Object { $_.is_primary } | Select-Object -First 1).id; user = $f.user }
}
Write-Host "Created $($children.Count) children with guardians."

# ── Accounts that line up with the login-screen test-user buttons (random passwords, discarded) ──
foreach ($s in $staff | Where-Object { $_.user }) {
    Try-Call 'POST' '/users' @{ email = "test.$($s.user)@dev.local"; first_name = 'Test'; last_name = $s.user; password = (Password); roles = @($s.user); staff_id = $s.id; preferred_language = 'en' } | Out-Null
}
foreach ($c in $children | Where-Object { $_.user }) {
    Try-Call 'POST' '/users' @{ email = "test.$($c.user)@dev.local"; first_name = 'Test'; last_name = 'parent'; password = (Password); roles = @('parent'); guardian_id = $c.mother; preferred_language = 'en' } | Out-Null
}
Write-Host 'Linked the test teacher, receptionist and parent buttons to real records.'

# ── Leave types and a few requests ──
$sick = Call 'POST' '/leave-types' @{ name = @{ en = "Sick$tag"; ar = 'مرضية' }; applies_to = 'both'; requires_approval = $false; affects_attendance = $true; affects_ratio = $true }
$travel = Call 'POST' '/leave-types' @{ name = @{ en = "Family travel$tag"; ar = 'سفر عائلي' }; applies_to = 'student'; requires_approval = $true; affects_attendance = $true; affects_ratio = $false }
Try-Call 'POST' '/leaves' @{ person_type = 'student'; person_id = $children[5].id; leave_type_id = $travel.id; start_date = (Get-Date).AddDays(2).ToString('yyyy-MM-dd'); end_date = (Get-Date).AddDays(6).ToString('yyyy-MM-dd'); reason = 'Visiting grandparents' } | Out-Null
Try-Call 'POST' '/leaves' @{ person_type = 'student'; person_id = $children[7].id; leave_type_id = $sick.id; start_date = (Get-Date).ToString('yyyy-MM-dd'); end_date = (Get-Date).AddDays(1).ToString('yyyy-MM-dd'); reason = 'Fever' } | Out-Null

# ── Today's attendance: most arrive, two staff in the first room (so it is short), one absence ──
foreach ($c in $children | Select-Object -First 4) { Try-Call 'POST' '/attendance/actions/check-in' @{ barcode = $c.barcode } | Out-Null }
foreach ($c in $children | Select-Object -Skip 4 -First 3) { Try-Call 'POST' '/attendance/actions/check-in' @{ barcode = $c.barcode } | Out-Null }
Try-Call 'POST' '/attendance/actions/mark-absent' @{ student_id = $children[8].id; notes = 'Parent called in' } | Out-Null
foreach ($s in $staff | Where-Object { $_.room } | Select-Object -First 3) {
    Try-Call 'POST' '/attendance/actions/check-in' @{ person_type = 'staff'; person_id = $s.id; room_id = $rooms[$s.room] } | Out-Null
}

# ── Feed and messages ──
Try-Call 'POST' '/activities' @{ type = 'diary'; caption = @{ en = 'We built a tall tower with blocks and sang the colours song.'; ar = 'بنينا برجاً عالياً من المكعبات وغنينا أغنية الألوان.' }; student_ids = @($children[0].id, $children[1].id); media_ids = @() } | Out-Null
Try-Call 'POST' '/activities' @{ type = 'food'; caption = @{ en = 'Lunch: chicken rice and steamed vegetables. Everyone ate well.'; ar = 'الغداء: أرز بالدجاج وخضار مطهوة. أكل الجميع جيداً.' }; student_ids = @($children[4].id); media_ids = @() } | Out-Null
Try-Call 'POST' '/activities' @{ type = 'announcement'; caption = @{ en = 'The centre will close early on Thursday at 12:00 for a staff training day.'; ar = 'سيُغلق المركز مبكراً يوم الخميس الساعة 12:00 ليوم تدريب الموظفين.' }; student_ids = @(); media_ids = @() } | Out-Null
Try-Call 'POST' '/messages/threads' @{ subject = 'Welcome to Bright Futures'; body = 'Welcome! You can message us here any time about your child.'; student_id = $children[0].id; broadcast = $false } | Out-Null
Try-Call 'POST' '/messages/threads' @{ subject = 'Eid holiday dates'; body = 'The centre is closed for the Eid holiday. We reopen the following Monday.'; broadcast = $true } | Out-Null

Write-Host ''
Write-Host 'Done. Test data loaded through the API:' -ForegroundColor Green
Write-Host "  3 rooms, 2 programmes, $($staff.Count) staff, $($children.Count) children with guardians, 2 leave types, today's attendance, feed posts, messages."
Write-Host '  Use the test-user buttons on the login screen (Teacher, Parent, Receptionist, Centre administrator, IT).'
Write-Host '  To remove it all: drop the NMS database and restart the API.'
