<#
.SYNOPSIS
  The only way to change the product version (ADR-0009).

.DESCRIPTION
  nms/VERSION holds the one version of the whole product (API, web app, mobile app, public website).
  This script raises it by the semantic-versioning rule, copies it into every place that shows a version,
  and adds the release notes to CHANGELOG.md. With -Check it only confirms everything agrees.

.EXAMPLE
  powershell -File tools\version.ps1 -Check
.EXAMPLE
  powershell -File tools\version.ps1 -Bump minor -Change "Gallery: share photos with chosen branches"
.EXAMPLE
  powershell -File tools\version.ps1 -Bump patch -Change "Fix: removing a guardian asks in the app's own box"
#>
param(
  [ValidateSet('major', 'minor', 'patch')]
  [string]$Bump,
  # One line per change, as people would read it in the release notes.
  [string[]]$Change = @(),
  [switch]$Check
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Text($relative) { [System.IO.File]::ReadAllText((Join-Path $root $relative), $utf8) }
function Write-Text($relative, $text) { [System.IO.File]::WriteAllText((Join-Path $root $relative), $text, $utf8) }

# Every place outside VERSION that carries the version, with how to find it there.
$places = @(
  @{ File = 'web\package.json'; Pattern = '("version":\s*")([^"]+)(")' },
  @{ File = 'mobile\package.json'; Pattern = '("version":\s*")([^"]+)(")' },
  @{ File = 'mobile\app.json'; Pattern = '("version":\s*")([^"]+)(")' },
  @{ File = 'website\index.html'; Pattern = '(<meta name="version" content=")([^"]+)(")' }
)

$current = (Read-Text 'VERSION').Trim()
if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "VERSION must be MAJOR.MINOR.PATCH, found '$current'." }

function Test-Versions($expected) {
  $wrong = @()
  foreach ($place in $places) {
    $match = [regex]::Match((Read-Text $place.File), $place.Pattern)
    if (-not $match.Success) { $wrong += "$($place.File): no version found" }
    elseif ($match.Groups[2].Value -ne $expected) { $wrong += "$($place.File): $($match.Groups[2].Value)" }
  }
  if (-not (Read-Text 'CHANGELOG.md').Contains("## [$expected]")) { $wrong += "CHANGELOG.md: no section for $expected" }
  return $wrong
}

if ($Check) {
  $wrong = Test-Versions $current
  if ($wrong.Count -gt 0) {
    Write-Host "Version $current is not applied everywhere:" -ForegroundColor Red
    $wrong | ForEach-Object { Write-Host "  $_" }
    exit 1
  }
  Write-Host "Version $current is applied everywhere."
  exit 0
}

if (-not $Bump) { throw 'Say -Bump major, minor or patch (or -Check).' }
if ($Change.Count -eq 0) { throw 'Describe the release with at least one -Change "...".' }
$wrong = Test-Versions $current
if ($wrong.Count -gt 0) { throw "Fix the current version first (run -Check): $($wrong -join '; ')" }

[int]$major = $Matches[1]; [int]$minor = $Matches[2]; [int]$patch = $Matches[3]
switch ($Bump) {
  'major' { $major++; $minor = 0; $patch = 0 }
  'minor' { $minor++; $patch = 0 }
  'patch' { $patch++ }
}
$next = "$major.$minor.$patch"

Write-Text 'VERSION' "$next`n"
foreach ($place in $places) {
  $text = Read-Text $place.File
  $text = [regex]::new($place.Pattern).Replace($text, { param($m) $m.Groups[1].Value + $next + $m.Groups[3].Value }, 1)
  Write-Text $place.File $text
}

# App stores only accept a higher build number for every release.
$app = Read-Text 'mobile\app.json'
$app = [regex]::new('("versionCode":\s*)(\d+)').Replace($app, { param($m) $m.Groups[1].Value + ([int]$m.Groups[2].Value + 1) }, 1)
$app = [regex]::new('("buildNumber":\s*")(\d+)(")').Replace($app, { param($m) $m.Groups[1].Value + ([int]$m.Groups[2].Value + 1) + $m.Groups[3].Value }, 1)
Write-Text 'mobile\app.json' $app

$changelog = Read-Text 'CHANGELOG.md'
$entry = "## [$next] - $(Get-Date -Format 'yyyy-MM-dd')`n`n" + (($Change | ForEach-Object { "- $_" }) -join "`n") + "`n`n"
$at = $changelog.IndexOf('## [')
$changelog = if ($at -ge 0) { $changelog.Insert($at, $entry) } else { $changelog.TrimEnd() + "`n`n" + $entry }
Write-Text 'CHANGELOG.md' $changelog

Write-Host "Version $current -> $next. Rebuild and redeploy so the API and apps report it."
