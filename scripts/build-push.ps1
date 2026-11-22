<#
.SYNOPSIS
    Build and optionally push the ManagerStudentCaltholic Docker image to a container registry (GHCR).

.DESCRIPTION
    Enforces the standardized image tag convention from So_Quan_Ly_Docker_Image_QLGL.xlsx:
      - Normal release : {name}-v1.{yy}.{phase}.{number}            (e.g. parish-app-v1.26.inf.1)
      - Hotfix release : {name}-v1.{yy}.{phase}-{hotfix}.{number}   (e.g. parish-app-v1.26.5-hf1.2)
    Full OCI reference : {registry}/{owner}/{name}:{tag}

.EXAMPLE
    powershell -File scripts/build-push.ps1 -Phase inf -Number 1 -DryRun

.EXAMPLE
    powershell -File scripts/build-push.ps1 -Phase 5 -Hotfix hf1 -Number 2 -Push
#>
[CmdletBinding()]
param(
    [string]$Name = "parish-app",
    [string]$Year = (Get-Date).ToString("yy"),
    [Parameter(Mandatory = $true)]
    [string]$Phase,
    [string]$Hotfix = "",
    [int]$Number = 1,
    [string]$Registry = "ghcr.io",
    [string]$Owner = "kiin11",
    [switch]$Push,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Normalize OCI lowercase fields
$Name = $Name.Trim().ToLowerInvariant()
$Year = $Year.Trim()
$Phase = $Phase.Trim().ToLowerInvariant()
$Hotfix = $Hotfix.Trim().ToLowerInvariant()
$Registry = $Registry.Trim().ToLowerInvariant()
$Owner = $Owner.Trim().ToLowerInvariant()

# Strict input validation
if ($Name -notmatch '^[a-z0-9][a-z0-9-]*$') {
    throw "Invalid -Name '$Name'. Expected lowercase alphanumeric and hyphens (e.g. 'parish-app')."
}
if ($Year -notmatch '^\d{2}$') {
    throw "Invalid -Year '$Year'. Expected 2-digit year (e.g. '26')."
}
if ($Phase -notmatch '^[a-z0-9]+$') {
    throw "Invalid -Phase '$Phase'. Expected lowercase alphanumeric token (e.g. 'inf', '5', '6')."
}
if (-not [string]::IsNullOrEmpty($Hotfix) -and ($Hotfix -notmatch '^[a-z0-9]+$')) {
    throw "Invalid -Hotfix '$Hotfix'. Expected lowercase alphanumeric token (e.g. 'hf1')."
}
if ($Number -lt 1) {
    throw "Invalid -Number '$Number'. Expected positive integer >= 1."
}
if ($Registry -notmatch '^[a-z0-9.-]+(:\d+)?$') {
    throw "Invalid -Registry '$Registry'. Expected registry hostname (e.g. 'ghcr.io')."
}
if ($Owner -notmatch '^[a-z0-9][a-z0-9._-]*$') {
    throw "Invalid -Owner '$Owner'. Expected lowercase registry owner/namespace (e.g. 'kiin11')."
}

# Build standardized tag and full OCI reference
if ([string]::IsNullOrEmpty($Hotfix)) {
    $Tag = "{0}-v1.{1}.{2}.{3}" -f $Name, $Year, $Phase, $Number
} else {
    $Tag = "{0}-v1.{1}.{2}-{3}.{4}" -f $Name, $Year, $Phase, $Hotfix, $Number
}

$FullImageRef = "{0}/{1}/{2}:{3}" -f $Registry, $Owner, $Name, $Tag
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$DockerfileRel = "ManagerStudentCaltholic/Dockerfile"
$DockerfileFull = Join-Path $RepoRoot $DockerfileRel

if (-not (Test-Path $DockerfileFull)) {
    throw "Dockerfile not found at '$DockerfileFull'."
}

# Resolve short git commit SHA (best-effort)
$GitSha = "unknown"
$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if ($null -ne $gitCmd) {
    try {
        $shaOut = & git -C $RepoRoot rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($shaOut)) {
            $GitSha = $shaOut.Trim()
        }
    } catch {
        $GitSha = "unknown"
    }
}

$BuildArgs = @(
    "build",
    "--platform", "linux/amd64",
    "-f", $DockerfileRel,
    "-t", $FullImageRef,
    "."
)
$BuildCmdDisplay = "docker " + ($BuildArgs -join " ")
$PushCmdDisplay = "docker push $FullImageRef"
$TodayStr = (Get-Date).ToString("yyyy-MM-dd")
$SizeDisplay = "TBD (dry-run)"
$StatusDisplay = if ($DryRun) { "DRY-RUN" } elseif ($Push) { "BUILT+PUSHED" } else { "BUILT-LOCAL" }

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " ManagerStudentCaltholic - Docker Image Build & Push" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " Repo Root      : $RepoRoot"
Write-Host " Dockerfile     : $DockerfileRel"
Write-Host " Git Commit     : $GitSha"
Write-Host " Image Tag      : $Tag"
Write-Host " Full Image Ref : $FullImageRef"
Write-Host " Push Enabled   : $($Push.IsPresent)"
Write-Host " Dry Run        : $($DryRun.IsPresent)"
Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkGray

if ($DryRun) {
    Write-Host "[DRY-RUN] Build command :" -ForegroundColor Yellow
    Write-Host "  $BuildCmdDisplay"
    if ($Push) {
        Write-Host "[DRY-RUN] Push command  :" -ForegroundColor Yellow
        Write-Host "  $PushCmdDisplay"
    }
} else {
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($null -eq $dockerCmd) {
        throw "Docker CLI not found in PATH. Install Docker Desktop or use GitHub Actions workflow '.github/workflows/build-push-ghcr.yml'."
    }

    Push-Location $RepoRoot
    try {
        Write-Host "[1/3] Running: $BuildCmdDisplay" -ForegroundColor Green
        & docker @BuildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed with exit code $LASTEXITCODE."
        }

        Write-Host "[2/3] Inspecting image size..." -ForegroundColor Green
        $sizeBytesRaw = & docker image inspect --format "{{.Size}}" $FullImageRef
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to inspect image size for '$FullImageRef'."
        }

        $sizeBytes = [double]$sizeBytesRaw.Trim()
        $sizeMb = [math]::Round($sizeBytes / 1MB, 2)
        $SizeDisplay = "{0} MB" -f $sizeMb

        if ($sizeMb -ge 250) {
            Write-Warning "Image size is $sizeMb MB (>= 250 MB target threshold)! Review Dockerfile layers and .dockerignore."
        } else {
            Write-Host "Image size OK: $sizeMb MB (< 250 MB target)." -ForegroundColor Green
        }

        if ($Push) {
            Write-Host "[3/3] Pushing image: $PushCmdDisplay" -ForegroundColor Green
            & docker push $FullImageRef
            if ($LASTEXITCODE -ne 0) {
                throw "Docker push failed with exit code $LASTEXITCODE. Ensure you ran 'docker login $Registry'."
            }
            Write-Host "Push completed successfully." -ForegroundColor Green
        } else {
            Write-Host "[3/3] Skipping push (pass -Push to publish to $Registry)." -ForegroundColor Yellow
        }
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "--- Ready-to-paste log line for So_Quan_Ly_Docker_Image_QLGL.xlsx ---" -ForegroundColor Cyan
Write-Host ("{0} | {1} | {2} | {3} | {4} | {5} | {6}" -f $TodayStr, $Tag, $FullImageRef, $SizeDisplay, $GitSha, $BuildCmdDisplay, $StatusDisplay)
Write-Host ""
Write-Host "--- Azure VM Deploy Command (in parish-infra-config) ----------------" -ForegroundColor Cyan
Write-Host "./scripts/deploy.sh $Tag"
Write-Host "# Or manual compose: set APP_IMAGE_TAG=$Tag in .env, then run:"
Write-Host "# docker compose pull dotnet_app && docker compose up -d --no-deps dotnet_app"
Write-Host "======================================================================" -ForegroundColor Cyan
