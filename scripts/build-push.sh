#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ManagerStudentCaltholic - Standardized Docker Image Build & Push Script
#
# Tag Standard (So_Quan_Ly_Docker_Image_QLGL.xlsx):
#   Normal : {name}-v1.{yy}.{phase}.{number}           (e.g. parish-app-v1.26.inf.1)
#   Hotfix : {name}-v1.{yy}.{phase}-{hotfix}.{number}  (e.g. parish-app-v1.26.5-hf1.2)
# Full Ref : {registry}/{owner}/{name}:{tag}
# ==============================================================================

NAME="parish-app"
YEAR="$(TZ=Asia/Ho_Chi_Minh date +%y 2>/dev/null || date +%y)"
PHASE=""
HOTFIX=""
NUMBER="1"
REGISTRY="ghcr.io"
OWNER="kiin11"
PUSH=false
DRY_RUN=false

usage() {
  cat <<EOF
Usage: $(basename "$0") --phase <phase> [options]

Options:
  --name <name>          Image name (default: parish-app)
  --year <yy>            2-digit year (default: current 2-digit year, e.g. 26)
  --phase <phase>        Release phase, required (e.g. inf, 5, 6)
  --hotfix <hotfix>      Optional hotfix suffix (e.g. hf1)
  --number <n>           Positive build sequence number (default: 1)
  --registry <host>      Container registry (default: ghcr.io)
  --owner <owner>        Registry owner/namespace (default: kiin11)
  --push                 Push image to registry after build
  --dry-run              Validate inputs and print commands without running docker
  -h, --help             Show this help message

Examples:
  ./scripts/build-push.sh --phase inf --number 1 --dry-run
  ./scripts/build-push.sh --phase 5 --hotfix hf1 --number 2 --push
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --name)     NAME="${2:?Missing value for --name}"; shift 2 ;;
    --year)     YEAR="${2:?Missing value for --year}"; shift 2 ;;
    --phase)    PHASE="${2:?Missing value for --phase}"; shift 2 ;;
    --hotfix)   HOTFIX="${2:-}"; shift 2 ;;
    --number)   NUMBER="${2:?Missing value for --number}"; shift 2 ;;
    --registry) REGISTRY="${2:?Missing value for --registry}"; shift 2 ;;
    --owner)    OWNER="${2:?Missing value for --owner}"; shift 2 ;;
    --push)     PUSH=true; shift ;;
    --dry-run)  DRY_RUN=true; shift ;;
    -h|--help)  usage; exit 0 ;;
    *)
      echo "ERROR: Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

# Normalize lowercase fields for OCI compatibility
NAME="$(printf '%s' "$NAME" | tr '[:upper:]' '[:lower:]')"
PHASE="$(printf '%s' "$PHASE" | tr '[:upper:]' '[:lower:]')"
HOTFIX="$(printf '%s' "$HOTFIX" | tr '[:upper:]' '[:lower:]')"
REGISTRY="$(printf '%s' "$REGISTRY" | tr '[:upper:]' '[:lower:]')"
OWNER="$(printf '%s' "$OWNER" | tr '[:upper:]' '[:lower:]')"

# Validate inputs
if [[ -z "$PHASE" ]]; then
  echo "ERROR: --phase is required (e.g. --phase inf)." >&2
  usage >&2
  exit 1
fi
if [[ ! "$NAME" =~ ^[a-z0-9][a-z0-9-]*$ ]]; then
  echo "ERROR: Invalid --name '$NAME'. Expected lowercase alphanumeric and hyphens." >&2
  exit 1
fi
if [[ ! "$YEAR" =~ ^[0-9]{2}$ ]]; then
  echo "ERROR: Invalid --year '$YEAR'. Expected 2-digit year (e.g. 26)." >&2
  exit 1
fi
if [[ ! "$PHASE" =~ ^[a-z0-9]+$ ]]; then
  echo "ERROR: Invalid --phase '$PHASE'. Expected lowercase alphanumeric token." >&2
  exit 1
fi
if [[ -n "$HOTFIX" && ! "$HOTFIX" =~ ^[a-z0-9]+$ ]]; then
  echo "ERROR: Invalid --hotfix '$HOTFIX'. Expected lowercase alphanumeric token (e.g. hf1)." >&2
  exit 1
fi
if [[ ! "$NUMBER" =~ ^[1-9][0-9]*$ ]]; then
  echo "ERROR: Invalid --number '$NUMBER'. Expected positive integer >= 1." >&2
  exit 1
fi
if [[ ! "$REGISTRY" =~ ^[a-z0-9.-]+(:[0-9]+)?$ ]]; then
  echo "ERROR: Invalid --registry '$REGISTRY'." >&2
  exit 1
fi
if [[ ! "$OWNER" =~ ^[a-z0-9][a-z0-9._-]*$ ]]; then
  echo "ERROR: Invalid --owner '$OWNER'." >&2
  exit 1
fi

# Generate standardized tag & full OCI image ref
if [[ -z "$HOTFIX" ]]; then
  TAG="${NAME}-v1.${YEAR}.${PHASE}.${NUMBER}"
else
  TAG="${NAME}-v1.${YEAR}.${PHASE}-${HOTFIX}.${NUMBER}"
fi

FULL_IMAGE_REF="${REGISTRY}/${OWNER}/${NAME}:${TAG}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOCKERFILE_REL="ManagerStudentCaltholic/Dockerfile"

if [[ ! -f "${REPO_ROOT}/${DOCKERFILE_REL}" ]]; then
  echo "ERROR: Dockerfile not found at ${REPO_ROOT}/${DOCKERFILE_REL}" >&2
  exit 1
fi

GIT_SHA="unknown"
if command -v git >/dev/null 2>&1; then
  GIT_SHA="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo "unknown")"
fi

BUILD_CMD="docker build --platform linux/amd64 -f ${DOCKERFILE_REL} -t ${FULL_IMAGE_REF} ."
PUSH_CMD="docker push ${FULL_IMAGE_REF}"
TODAY_STR="$(TZ=Asia/Ho_Chi_Minh date +%Y-%m-%d 2>/dev/null || date +%Y-%m-%d)"
SIZE_DISPLAY="TBD (dry-run)"
if [[ "$DRY_RUN" == "true" ]]; then
  STATUS_DISPLAY="DRY-RUN"
elif [[ "$PUSH" == "true" ]]; then
  STATUS_DISPLAY="BUILT+PUSHED"
else
  STATUS_DISPLAY="BUILT-LOCAL"
fi

echo "======================================================================"
echo " ManagerStudentCaltholic - Docker Image Build & Push"
echo "======================================================================"
echo " Repo Root      : ${REPO_ROOT}"
echo " Dockerfile     : ${DOCKERFILE_REL}"
echo " Git Commit     : ${GIT_SHA}"
echo " Image Tag      : ${TAG}"
echo " Full Image Ref : ${FULL_IMAGE_REF}"
echo " Push Enabled   : ${PUSH}"
echo " Dry Run        : ${DRY_RUN}"
echo "----------------------------------------------------------------------"

if [[ "$DRY_RUN" == "true" ]]; then
  echo "[DRY-RUN] Build command :"
  echo "  ${BUILD_CMD}"
  if [[ "$PUSH" == "true" ]]; then
    echo "[DRY-RUN] Push command  :"
    echo "  ${PUSH_CMD}"
  fi
else
  if ! command -v docker >/dev/null 2>&1; then
    echo "ERROR: docker CLI not found. Install Docker or trigger '.github/workflows/build-push-ghcr.yml' on GitHub Actions." >&2
    exit 1
  fi

  cd "$REPO_ROOT"
  echo "[1/3] Running: ${BUILD_CMD}"
  docker build --platform linux/amd64 -f "${DOCKERFILE_REL}" -t "${FULL_IMAGE_REF}" .

  echo "[2/3] Inspecting image size..."
  SIZE_BYTES="$(docker image inspect --format '{{.Size}}' "${FULL_IMAGE_REF}")"
  SIZE_MB="$(awk -v b="$SIZE_BYTES" 'BEGIN { printf "%.2f", b / 1048576 }')"
  SIZE_DISPLAY="${SIZE_MB} MB"

  IS_OVERSIZE="$(awk -v mb="$SIZE_MB" 'BEGIN { print (mb >= 250.0) ? "1" : "0" }')"
  if [[ "$IS_OVERSIZE" == "1" ]]; then
    echo "WARNING: Image size is ${SIZE_MB} MB (>= 250 MB target threshold)! Review Dockerfile layers and .dockerignore." >&2
  else
    echo "Image size OK: ${SIZE_MB} MB (< 250 MB target)."
  fi

  if [[ "$PUSH" == "true" ]]; then
    echo "[3/3] Pushing image: ${PUSH_CMD}"
    docker push "${FULL_IMAGE_REF}"
    echo "Push completed successfully."
  else
    echo "[3/3] Skipping push (pass --push to publish to ${REGISTRY})."
  fi
fi

echo ""
echo "--- Ready-to-paste log line for So_Quan_Ly_Docker_Image_QLGL.xlsx ---"
echo "${TODAY_STR} | ${TAG} | ${FULL_IMAGE_REF} | ${SIZE_DISPLAY} | ${GIT_SHA} | ${BUILD_CMD} | ${STATUS_DISPLAY}"
echo ""
echo "--- Azure VM Deploy Command (in parish-infra-config) ----------------"
echo "./scripts/deploy.sh ${TAG}"
echo "# (.env variable: APP_IMAGE_TAG=${TAG})"
echo "======================================================================"
