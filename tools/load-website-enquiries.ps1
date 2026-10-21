<#
  Sends test enquiries to a LOCAL development system through the same public endpoint the nursery website form uses
  (no sign-in), so the pipeline in NMS shows website enquiries. Nothing ships with the app; drop the NMS database to remove them.

  Usage:  powershell -File tools\load-website-enquiries.ps1 [-Api http://localhost:5178] [-Centre al-salam-early-childhood]
#>
param([string]$Api = 'http://localhost:5178', [string]$Centre = 'al-salam-early-childhood')
$ErrorActionPreference = 'Stop'
$families = @(
    @('Fatima Al Hammadi', 'Omar', 'en', '+971501110001', 'fatima.web1@example.com', '2024-05-02'),
    @('Rashid Al Nuaimi', 'Salma', 'en', '+971501110002', '', '2023-11-20'),
    @('Aisha Khan', 'Ibrahim', 'en', '', 'aisha.web3@example.com', '2025-02-14'),
    @('Khalid Al Ketbi', 'Mariam', 'en', '+971501110004', 'khalid.web4@example.com', '2024-08-30'),
    @('Noor Al Shehhi', 'Hamad', 'en', '+971501110005', '', '2024-01-09'),
    @([string]::Join('', [char[]](0x0645,0x0631,0x064A,0x0645,0x0020,0x0627,0x0644,0x0645,0x0646,0x0635,0x0648,0x0631,0x064A)), [string]::Join('', [char[]](0x062E,0x0627,0x0644,0x062F)), 'ar', '+971501110006', '', '2024-06-18'),
    @('Sara Johnson', 'Liam', 'en', '', 'sara.web7@example.com', '2025-03-03'),
    @('Mohammed Al Blooshi', 'Reem', 'en', '+971501110008', 'mohammed.web8@example.com', '2023-12-25'),
    @('Layla Haddad', 'Adam', 'en', '+971501110009', '', '2024-09-12'),
    @('Yousef Al Dhaheri', 'Hind', 'en', '+971501110010', 'yousef.web10@example.com', '2024-04-04')
)
$sent = 0
foreach ($f in $families) {
    $payload = [ordered]@{
        family_name = $f[0]; child_name = $f[1]; phone = $(if ($f[3]) { $f[3] } else { $null }); email = $(if ($f[4]) { $f[4] } else { $null })
        preferred_contact = $(if ($f[3]) { 'phone' } else { 'email' }); child_date_of_birth = $f[5]; desired_start_date = $null
        message = 'Sent from the nursery website (test data)'; website = ''; captcha_token = $null
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress))
    try {
        Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Api/api/v1/public/enquiries/$Centre" -ContentType 'application/json; charset=utf-8' -Body $bytes | Out-Null
        $sent++
    } catch { Write-Host "  (skipped: $($_.Exception.Message))" -ForegroundColor DarkYellow }
    Start-Sleep -Seconds 13   # the public form allows only a few sends a minute
}
Write-Host "Done: $sent website enquiries sent."
