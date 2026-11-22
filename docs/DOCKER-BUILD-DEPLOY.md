# Docker Image Build, Registry (GHCR) & Deployment Guide

This repository (`ManagerStudentCaltholic`) contains **application source code and build definitions only**.
Production infrastructure (`docker-compose.yml`, Nginx reverse proxy configs, `.env`, and server deploy/rollback scripts) is maintained separately in the private **`parish-infra-config`** repository.

---

## 1. Standardized Image Tag Convention

All production and staging images follow the exact naming contract tracked in `So_Quan_Ly_Docker_Image_QLGL.xlsx`:

| Release Type | Tag Format | Example |
|---|---|---|
| Normal release | `{name}-v1.{yy}.{phase}.{number}` | `parish-app-v1.26.inf.1` |
| Hotfix release | `{name}-v1.{yy}.{phase}-{hotfix}.{number}` | `parish-app-v1.26.5-hf1.2` |

- **Full OCI Image Reference:** `ghcr.io/kiin11/parish-app:<TAG>`
  - Example: `ghcr.io/kiin11/parish-app:parish-app-v1.26.inf.1`
- **Target Platform:** `linux/amd64` (Azure Standard B2ats v2 VM)
- **Target Runtime Size:** `< 250 MB` (`mcr.microsoft.com/dotnet/aspnet:8.0-alpine` with `tzdata`, `icu-libs`, `icu-data-full`, `fontconfig`, `ttf-dejavu`)
- **Container Runtime Contract:** non-root `$APP_UID`, internal port `5000`, mounted paths `/app/dataprotection-keys`, `/app/logs`, `/app/secrets`, `/app/wwwroot/uploads/lesson_plans`.

---

## 2. GHCR Authentication (Personal Access Token)

Never commit tokens, `.env`, or `appsettings.Development.json` into Git.

### Local Build Machine (push access)
Create a GitHub Classic PAT with `write:packages` (and `read:packages`), then authenticate Docker CLI:

```powershell
# PowerShell
$env:GHCR_PAT | docker login ghcr.io -u kiin11 --password-stdin
```

```bash
# Bash
echo "$GHCR_PAT" | docker login ghcr.io -u kiin11 --password-stdin
```

### Azure Production VM (read-only pull access)
Create a separate read-only GitHub Classic PAT with **`read:packages` only**:

```bash
echo "$GHCR_READ_PAT" | docker login ghcr.io -u kiin11 --password-stdin
```

---

## 3. Option A: Build & Push via GitHub Actions (Recommended — No Local Docker Required)

Use the manual workflow [`.github/workflows/build-push-ghcr.yml`](../.github/workflows/build-push-ghcr.yml) whenever your local PC does not have Docker Desktop installed or you want clean cloud builds:

1. Open the repository on GitHub -> **Actions** -> **Build and Push Docker Image to GHCR**.
2. Click **Run workflow** and provide:
   - `phase`: e.g. `inf`, `5`, `6`
   - `hotfix`: leave blank for normal releases, or enter e.g. `hf1`
   - `number`: build number (e.g. `1`, `2`)
   - `year`: optional 2-digit year (defaults to current `Asia/Ho_Chi_Minh` year)
   - `push`: keep checked (`true`) to publish to `ghcr.io`
3. Wait for the job to finish (~1–2 min with GitHub Actions layer cache).
4. Copy the generated log line from the **Job Summary** into `So_Quan_Ly_Docker_Image_QLGL.xlsx` and copy the deploy command for Azure VM.

> GitHub Actions automatically authenticates to GHCR using the ephemeral `GITHUB_TOKEN` (`packages: write`) — no manual secret setup is required.

---

## 4. Option B: Local Build & Push Scripts

Both scripts build from repository root context (`.`) using `-f ManagerStudentCaltholic/Dockerfile` for `--platform linux/amd64`, inspect image size (warning if `>= 250 MB`), optionally push, and print the Excel tracking line + Azure deploy command.

### Windows (PowerShell)

```powershell
# Dry-run tag & command verification (does not require Docker)
powershell -File scripts/build-push.ps1 -Phase inf -Number 1 -DryRun
powershell -File scripts/build-push.ps1 -Phase 5 -Hotfix hf1 -Number 2 -DryRun

# Build locally only
powershell -File scripts/build-push.ps1 -Phase inf -Number 1

# Build and push to GHCR
powershell -File scripts/build-push.ps1 -Phase inf -Number 1 -Push
```

### Linux / macOS / WSL (Bash)

```bash
# Dry-run
./scripts/build-push.sh --phase inf --number 1 --dry-run

# Build and push hotfix image
./scripts/build-push.sh --phase 5 --hotfix hf1 --number 2 --push
```

---

## 5. Deploy on Azure VM

On the Azure VM inside the private `parish-infra-config` directory:

```bash
./scripts/deploy.sh parish-app-v1.26.inf.1
```

To roll back to the previous known-good image in `< 30s`:

```bash
./scripts/rollback.sh
```
