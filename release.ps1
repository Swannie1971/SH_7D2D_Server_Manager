<#
  Release helper for 7 Days to Die - Server Manager.

  Run it and answer the prompts:

      .\release.ps1

  Steps, in order. Each one asks first, so you can run only the parts you need:

      1. Publish        -> the single self-contained .exe
      2. Installer      -> the Inno Setup wizard (needs step 1)
      3. Push code      -> merge the branch into main and push
      4. GitHub release -> upload BOTH exes and tag it

  The version is read out of UpdateService.cs, which is the app's single source of
  truth - so the tag, the installer and what the app reports about itself can never
  disagree.

  -Yes runs every step without prompting.

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
$m = Select-String -Path $versionFile -Pattern 'CurrentVersion\s*=\s*"([^"]+)"'
if (-not $m) {
    Write-Fail "Could not find CurrentVersion in $versionFile"
    exit 1
}
$version = $m.Matches[0].Groups[1].Value
$tag     = "v$version"

# The .iss carries its own copy, used for the installer's filename. Warn loudly if
# they drift - a mismatch means the installer is named after the wrong version.
$issMatch = Select-String -Path $issFile -Pattern '#define\s+MyAppVersion\s+"([^"]+)"'
$issVer   = $issMatch.Matches[0].Groups[1].Value
$setupExe = Join-Path $repo "installer\output\SevenDaysManager-Setup-$issVer.exe"

Write-Host ""
Write-Host "  7 Days to Die - Server Manager   release $tag" -ForegroundColor White
Write-Host "  ---------------------------------------------"
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

    if ($dirty) {
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
