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
- **Container Runtime Contract:** non-root `$APP_UID`, internal port `8080` (`ASPNETCORE_HTTP_PORTS=8080`, standardized compose service name `dotnet_app`), HTTP healthcheck `GET /health`, mounted persistent paths `/app/PrivateUploads`, `/app/dataprotection-keys`, `/app/logs`, `/app/secrets`, `/app/wwwroot/uploads/lesson_plans`.

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

Or manually (after updating `APP_IMAGE_TAG=parish-app-v1.26.inf.1` in `.env`):

```bash
docker compose pull dotnet_app && docker compose up -d --no-deps dotnet_app
```

To roll back to the previous known-good image in `< 30s`:

```bash
./scripts/rollback.sh
```

---

## 6. Production Security & Persistence Hardening (`parish-infra-config`)

When configuring `docker-compose.yml` and `.env` in `parish-infra-config`:

1. **Standardized Compose Service Name:** Always name the application service `dotnet_app` (never `app`).
2. **Mandatory Persistence Volume Mounts:**
   - `/app/PrivateUploads` (exports, fallback cloud storage, lesson plans, temp directories)
   - `/app/dataprotection-keys` (Cookie & Anti-CSRF key ring persistence)
   - `/app/logs` (Serilog rolling logs)
   - `/app/secrets` (Google Drive service account JSON mounted read-only)
   - `/app/wwwroot/uploads/lesson_plans`
3. **PostgreSQL TLS Verification:**
   - Configure `SSL Mode=VerifyCA` or `SSL Mode=VerifyFull` with your CA certificate in `ConnectionStrings__DefaultConnection`.
   - **Do NOT** use `Trust Server Certificate=true` in Production (bypasses server certificate validation and enables MITM).
4. **Redis Authentication & Network Isolation:**
   - Start Redis with `--requirepass ${REDIS_PASSWORD}` and include `,password=${REDIS_PASSWORD}` in `ConnectionStrings__RedisConnection`.
   - **Do NOT** publish/expose port `6379` on the public host interface (`ports: ["6379:6379"]`); keep Redis reachable only on the internal Docker bridge network.
5. **Container Healthcheck & Reverse Proxy:**
   - The Dockerfile defines `HEALTHCHECK` against `GET http://127.0.0.1:8080/health`.
   - Configure `ReverseProxy:ForwardLimit`, `ReverseProxy:KnownProxies`, and `ReverseProxy:KnownNetworks` narrowly to match your Nginx container/network.
