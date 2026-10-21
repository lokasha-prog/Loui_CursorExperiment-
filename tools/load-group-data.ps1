<#
  Test data for the multi-centre group, through the API on a LOCAL development system.
  Moves a few enrolled children to the other centres of the group and gives a few teachers a second centre.
  If the signed-in test user has no branch yet it creates two test branches. Nothing ships with the app.

  Usage:  powershell -File tools\load-group-data.ps1 [-Api http://localhost:5178] [-Children 10] [-TeacherCount 3]
#>
param([string]$Api = 'http://localhost:5178', [int]$Children = 10, [int]$TeacherCount = 3)
$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$headers = @{}

function Call($method, $path, $body = $null) {
    $args = @{ Uri = "$Api/api/v1$path"; Method = $method; WebSession = $session; Headers = $headers; ContentType = 'application/json; charset=utf-8'; UseBasicParsing = $true }
    if ($null -ne $body) { $args.Body = [System.Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8 -Compress)) }
    try { $r = Invoke-WebRequest @args } catch {
        $detail = if ($_.ErrorDetails) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        throw "$method $path failed: $detail"
    }
    if ($r.Content) { ($r.Content | ConvertFrom-Json).data }
}

$login = Call 'POST' '/dev/quick-login' @{ role = 'it' }
$headers['Authorization'] = "Bearer $($login.access_token)"
Write-Host 'Signed in as the local test IT administrator.'

$mine = @(Call 'GET' '/group/my-centres')
$others = @($mine | Where-Object { -not $_.is_head })
if ($others.Count -eq 0) {
    # The test IT user is a different person from whoever made the branches by hand, so it makes its own two test branches.
    $stamp = (Get-Date).ToString('MMddHHmm')
    foreach ($n in 'North', 'South') {
        $null = Call 'POST' '/group/centres' @{ name = "Test Branch $n"; name_arabic = "فرع تجريبي $n"; slug = "test-branch-$($n.ToLower())-$stamp" }
    }
    $mine = @(Call 'GET' '/group/my-centres')
    $others = @($mine | Where-Object { -not $_.is_head })
}
Write-Host "Branch centres: $($others.name.en -join ', ')"

# Children: enrolled ones, shared out between the branches.
$kids = @(Call 'GET' '/students?status=active&limit=100')
$moved = 0
for ($i = 0; $i -lt [Math]::Min($Children, $kids.Count); $i++) {
    $to = $others[$i % $others.Count]
    $null = Call 'POST' "/group/students/$($kids[$i].id)/transfer" @{ to_centre_id = $to.id; reason = 'Test move between centres' }
    $moved++
}
Write-Host "Moved $moved children."

# Teachers: give each a second centre with the same role.
$users = @(Call 'GET' '/users?limit=100')
$teachers = @($users | Where-Object { $_.roles -contains 'teacher' })
$given = 0
for ($i = 0; $i -lt [Math]::Min($TeacherCount, $teachers.Count); $i++) {
    $to = $others[$i % $others.Count]
    try { $null = Call 'POST' '/group/staff-assignments' @{ email = $teachers[$i].email; centre_id = $to.id; role = 'teacher' }; $given++ }
    catch { Write-Host "  (skipped $($teachers[$i].email): already there or not allowed)" -ForegroundColor DarkYellow }
}
Write-Host "Gave $given teachers a second centre."

$overview = Call 'GET' '/group/overview'
$overview.centres | ForEach-Object { Write-Host ("  {0,-40} children {1,4}  staff {2,3}" -f $_.name.en, $_.students, $_.staff) }
