<#
  Release helper for 7 Days to Die - Server Manager.

  Run it and answer the prompts:

      .\release.ps1

  Steps, in order. Each one asks first, so you can run only the parts you need:

      0. Bump version   -> shows the current one, writes the new one to all 3 files
      1. Publish        -> the single self-contained .exe
      2. Installer      -> the Inno Setup wizard (needs step 1)
      3. Push code      -> merge the branch into main and push
      4. GitHub release -> upload BOTH exes and tag it

  The version is read out of UpdateService.cs, which is the app's single source of
  truth - so the tag, the installer and what the app reports about itself can never
  disagree. Step 0 keeps the csproj and the .iss in step with it.

  Step 0 does NOT commit. Step 3 refuses a dirty tree, so commit the bump before you
  get there (or answer no to step 3 and commit it yourself).

  -Yes runs every step without prompting, and SKIPS step 0 - a version number can only
  come from a human.

  NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads a BOM-less script as
  ANSI, so UTF-8 box-drawing characters get mangled and the script fails to parse.
#>

[CmdletBinding()]
param(
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot

# ---- Paths ------------------------------------------------------------------
$csproj      = Join-Path $repo 'SevenDaysManager\SevenDaysManager.csproj'
$versionFile = Join-Path $repo 'SevenDaysManager\Services\UpdateService.cs'
$issFile     = Join-Path $repo 'installer\sevendays.iss'
$publishExe  = Join-Path $repo 'SevenDaysManager\bin\Release\net9.0-windows\win-x64\publish\SevenDaysManager.exe'
$iscc        = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$gh          = 'C:\Program Files\GitHub CLI\gh.exe'
$releaseRepo = 'Swannie1971/SH_7D2D_Manager-releases'

# ---- Helpers ----------------------------------------------------------------
function Write-Step ($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-Ok   ($m) { Write-Host "  OK   $m" -ForegroundColor Green }
function Write-Warn ($m) { Write-Host "  WARN $m" -ForegroundColor Yellow }
function Write-Fail ($m) { Write-Host "  FAIL $m" -ForegroundColor Red }

function Ask ($question) {
    if ($Yes) { return $true }
    while ($true) {
        $a = Read-Host "$question [y/n]"
        if ($a -match '^(y|yes)$') { return $true }
        if ($a -match '^(n|no)$')  { return $false }
    }
}

# ---- Version: read it from the app, don't retype it --------------------------
function Read-AppVersion {
    $m = Select-String -Path $versionFile -Pattern 'CurrentVersion\s*=\s*"([^"]+)"'
    if (-not $m) {
        Write-Fail "Could not find CurrentVersion in $versionFile"
        exit 1
    }
    return $m.Matches[0].Groups[1].Value
}

function Read-IssVersion {
    $m = Select-String -Path $issFile -Pattern '#define\s+MyAppVersion\s+"([^"]+)"'
    if (-not $m) {
        Write-Fail "Could not find MyAppVersion in $issFile"
        exit 1
    }
    return $m.Matches[0].Groups[1].Value
}

# Rewrites the version in all three files that carry it. UpdateService.cs is the source
# of truth the rest of this script reads; the other two only matter for the exe's file
# properties and the installer's filename, but they must not drift out of step.
function Set-AppVersion ($new) {
    # UpdateService.cs - the source of truth
    (Get-Content $versionFile -Raw) `
        -replace '(CurrentVersion\s*=\s*")[^"]+(")', "`${1}$new`${2}" |
        Set-Content $versionFile -Encoding utf8 -NoNewline

    # csproj - Version is 3-part, Assembly/FileVersion are 4-part
    (Get-Content $csproj -Raw) `
        -replace '(<Version>)[^<]+(</Version>)',                 "`${1}$new`${2}" `
        -replace '(<AssemblyVersion>)[^<]+(</AssemblyVersion>)', "`${1}$new.0`${2}" `
        -replace '(<FileVersion>)[^<]+(</FileVersion>)',         "`${1}$new.0`${2}" |
        Set-Content $csproj -Encoding utf8 -NoNewline

    # installer
    (Get-Content $issFile -Raw) `
        -replace '(#define\s+MyAppVersion\s+")[^"]+(")', "`${1}$new`${2}" |
        Set-Content $issFile -Encoding utf8 -NoNewline
}

$version = Read-AppVersion
$issVer  = Read-IssVersion

Write-Host ""
Write-Host "  7 Days to Die - Server Manager" -ForegroundColor White
Write-Host "  ---------------------------------------------"
Write-Host "  current version                : $version" -ForegroundColor Cyan
Write-Host ""

# ---- 0. Bump the version ----------------------------------------------------
# Skipped under -Yes: the new number can only come from a human, and Ask would return
# $true and then block on Read-Host, hanging an unattended run.
if (-not $Yes -and (Ask "0. Bump the version?")) {
    while ($true) {
        $new = (Read-Host "   New version (blank = keep $version)").Trim()
        if ([string]::IsNullOrWhiteSpace($new)) { break }

        # x.y or x.y.z only - the csproj appends a 4th part for Assembly/FileVersion,
        # and the tag has to parse as a [Version] for the update check to compare it.
        if ($new -notmatch '^\d+\.\d+(\.\d+)?$') {
            Write-Warn "Use a numeric version like 0.3.3"
            continue
        }
        if ($new -eq $version) { break }

        Set-AppVersion $new
        $version = Read-AppVersion
        $issVer  = Read-IssVersion

        if ($version -ne $new) {
            Write-Fail "Version did not take - $versionFile still reads $version"
            exit 1
        }

        Write-Ok "bumped to $version"
        Write-Host "       UpdateService.cs, SevenDaysManager.csproj, sevendays.iss"

        # Offer to commit it here, because step 3 refuses a dirty tree - leaving the bump
        # uncommitted means the run cannot reach the push without you stopping to do it by
        # hand. Only the three version files are staged, BY PATH: any other work in progress
        # is none of this commit's business and must not be swept in.
        if (Ask "   Commit the bump?") {
            git -C $repo add -- $versionFile $csproj $issFile
            if ($LASTEXITCODE -ne 0) { Write-Fail "git add failed"; exit 1 }

            git -C $repo commit -m "Bump version to $version"
            if ($LASTEXITCODE -ne 0) { Write-Fail "git commit failed"; exit 1 }

            Write-Ok "committed: Bump version to $version"

            $others = git -C $repo status --porcelain
            if ($others) {
                Write-Warn "other changes are still uncommitted - step 3 will refuse them:"
                git -C $repo status --short
            }
        }
        else {
            Write-Warn "the bump is not committed - step 3 will refuse a dirty tree"
        }
        break
    }
}

$tag      = "v$version"
$setupExe = Join-Path $repo "installer\output\SevenDaysManager-Setup-$issVer.exe"

Write-Host ""
Write-Host "  releasing $tag" -ForegroundColor White
Write-Host "  app version (UpdateService.cs) : $version"
Write-Host "  installer version (.iss)       : $issVer"

if ($version -ne $issVer) {
    Write-Warn "VERSION MISMATCH - set MyAppVersion in installer\sevendays.iss to $version"
    $go = Ask "  Continue anyway?"
    if (-not $go) { exit 1 }
}

$branch = (git -C $repo rev-parse --abbrev-ref HEAD).Trim()
$dirty  = git -C $repo status --porcelain
Write-Host "  branch                         : $branch"
if ($dirty) { Write-Warn "working tree has uncommitted changes" }
Write-Host ""

# ---- 1. Publish -------------------------------------------------------------
$doPublish = Ask "1. Publish the single-file exe?"
if ($doPublish) {
    Write-Step "dotnet publish"
    dotnet publish $csproj -c Release
    if ($LASTEXITCODE -ne 0) { Write-Fail "publish failed"; exit 1 }

    if (-not (Test-Path $publishExe)) {
        Write-Fail "expected the exe at:"
        Write-Host  "       $publishExe"
        Write-Host  "       (check PublishSingleFile / RuntimeIdentifier in the csproj)"
        exit 1
    }
    $mb = [math]::Round((Get-Item $publishExe).Length / 1MB, 1)
    Write-Ok "$publishExe"
    Write-Host "       size: $mb MB"
}

# ---- 2. Installer -----------------------------------------------------------
$doInstaller = Ask "2. Build the Inno Setup installer?"
if ($doInstaller) {
    Write-Step "Inno Setup"

    if (-not (Test-Path $iscc)) {
        Write-Fail "ISCC.exe not found at $iscc"
        exit 1
    }
    if (-not (Test-Path $publishExe)) {
        Write-Fail "no published exe - run step 1 first"
        exit 1
    }

    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) { Write-Fail "ISCC failed"; exit 1 }

    if (-not (Test-Path $setupExe)) {
        Write-Fail "expected the installer at:"
        Write-Host  "       $setupExe"
        exit 1
    }
    $mb = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Ok "$setupExe"
    Write-Host "       size: $mb MB"
}

# ---- 3. Push the code -------------------------------------------------------
$doPush = Ask "3. Merge '$branch' into main and push?"
if ($doPush) {
    Write-Step "git"

    # Re-read rather than trusting the $dirty snapshot from startup: step 0 may have
    # committed the bump since, and a publish + installer build is long enough that you
    # could well have committed something in another window while it ran.
    if (git -C $repo status --porcelain) {
        Write-Fail "working tree is dirty - commit or stash first"
        git -C $repo status --short
        exit 1
    }

    if ($branch -ne 'main') {
        git -C $repo checkout main
        if ($LASTEXITCODE -ne 0) { exit 1 }

        git -C $repo merge $branch
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "merge conflict - resolve it, then re-run"
            exit 1
        }
    }

    git -C $repo push origin main
    if ($LASTEXITCODE -ne 0) { exit 1 }
    Write-Ok "pushed main"
}

# ---- 4. GitHub release ------------------------------------------------------
$doRelease = Ask "4. Create GitHub release $tag (uploads the bare exe)?"
if ($doRelease) {
    Write-Step "gh release create $tag"

    if (-not (Test-Path $gh)) {
        Write-Fail "gh.exe not found at $gh"
        exit 1
    }
    if (-not (Test-Path $publishExe)) {
        Write-Fail "no published exe - run step 1 first"
        exit 1
    }

    # ONLY the bare updater exe goes on GitHub. The installer is for distribution
    # (you hand it out yourself); it deliberately stays OFF the release so the in-app
    # updater can never download it by mistake and launch a setup wizard mid-update.
    $assets = @($publishExe)

    # Does the tag already exist? gh writes "release not found" to STDERR when it
    # doesn't - and with ErrorActionPreference=Stop, PowerShell turns a native
    # command's stderr into a terminating error. So the "not found" case (the normal
    # one!) would kill the script. Swallow stderr and judge purely by the exit code.
    $ErrorActionPreference = 'Continue'
    & $gh release view $tag --repo $releaseRepo *> $null
    $exists = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'

    if ($exists) {
        Write-Warn "release $tag already exists on $releaseRepo"
        $recreate = Ask "  Delete and recreate it?"
        if (-not $recreate) { exit 0 }
        & $gh release delete $tag --repo $releaseRepo --yes
    }

    # NB: this release carries the bare updater exe only, so the notes must NOT tell
    # people to download a Setup file from here - it isn't attached. First-time users
    # get the installer from you directly.
    $notes = Read-Host "Release notes (blank = default)"
    if ([string]::IsNullOrWhiteSpace($notes)) {
        $notes = "Auto-update payload for existing installs. Existing installs update themselves on launch."
    }

    # Build the argument list explicitly. Passing @($a,$b) as a bare token hands gh ONE
    # argument containing both paths, so only the first asset ever got uploaded.
    $ghArgs = @('release', 'create', $tag)
    $ghArgs += $assets
    $ghArgs += @('--repo', $releaseRepo, '--title', $tag, '--notes', $notes)

    Write-Host "  uploading $($assets.Count) asset(s):"
    $assets | ForEach-Object { Write-Host "    - $(Split-Path $_ -Leaf)" }

    & $gh @ghArgs
    if ($LASTEXITCODE -ne 0) { Write-Fail "gh release create failed"; exit 1 }

    Write-Ok "https://github.com/$releaseRepo/releases/tag/$tag"
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host ""
