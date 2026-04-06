#!/usr/bin/env bash
# scripts/build.sh — Build Nocturne containers locally or in CI.
#
# Usage:
#   ./scripts/build.sh                    # build with tag "dev", no push
#   ./scripts/build.sh v1.2.3             # build with tag "v1.2.3", no push
#   ./scripts/build.sh latest --push      # build and push with tag "latest"
#
# Environment variables (optional):
#   REGISTRY          Container registry   (default: ghcr.io)
#   IMAGE_REPOSITORY  Image repository     (default: detected from git remote)
#   SKIP_API          Skip API container    (default: false)
#   SKIP_WEB          Skip Web container    (default: false)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION="${1:-dev}"
PUSH=""
if [[ "${2:-}" == "--push" ]]; then
  PUSH="--push"
fi

REGISTRY="${REGISTRY:-ghcr.io}"
if [[ -z "${IMAGE_REPOSITORY:-}" ]]; then
  IMAGE_REPOSITORY=$(git -C "$REPO_ROOT" remote get-url origin | sed -E 's|.*github\.com[:/]||;s|\.git$||' | tr '[:upper:]' '[:lower:]')
fi

echo "==> Build configuration"
echo "    Version:    $VERSION"
echo "    Registry:   $REGISTRY"
echo "    Repository: $IMAGE_REPOSITORY"
echo "    Push:       ${PUSH:-no}"
echo ""

# ---------------------------------------------------------------------------
# Step 1: Prepare — appsettings, restore, web dependencies
# ---------------------------------------------------------------------------
echo "==> Preparing build environment"
cd "$REPO_ROOT"

if [[ ! -f appsettings.json ]]; then
  echo "    Copying appsettings.example.json -> appsettings.json"
  cp appsettings.example.json appsettings.json
fi

echo "    Restoring .NET dependencies"
dotnet restore --verbosity quiet

echo "    Installing web dependencies"
(cd src/Web && pnpm install --frozen-lockfile)

echo "    Building bridge package"
(cd src/Web/packages/bridge && pnpm run build)

# ---------------------------------------------------------------------------
# Step 2: Generate API client (NSwag + Zod + remote codegen)
# ---------------------------------------------------------------------------
# IMPORTANT: This must run BEFORE dotnet publish, because publish marks the
# build outputs as up-to-date, causing a subsequent dotnet build to skip
# the post-build generation targets (NSwag, Zod, remote codegen).
echo "==> Generating API client"
dotnet build -c Release src/API/Nocturne.API/Nocturne.API.csproj --verbosity quiet

# ---------------------------------------------------------------------------
# Step 3: Verify generated files
# ---------------------------------------------------------------------------
echo "==> Verifying generated API client files"
GENERATED_DIR="src/Web/packages/app/src/lib/api/generated"
if [[ ! -f "$GENERATED_DIR/passkeys.generated.remote.ts" ]]; then
  echo "ERROR: Generated API client files are missing. NSwag/remote codegen may have failed." >&2
  ls -la "$GENERATED_DIR/" 2>/dev/null || echo "  Generated directory does not exist" >&2
  exit 1
fi
REMOTE_COUNT=$(find "$GENERATED_DIR" -name "*.generated.remote.ts" | wc -l)
echo "    Found $REMOTE_COUNT generated remote files"

# Clean up root appsettings.json to avoid NETSDK1152 conflict with
# src/API/Nocturne.API/appsettings.json during dotnet publish
if [[ -f "$REPO_ROOT/appsettings.json" && -f "$REPO_ROOT/appsettings.example.json" ]]; then
  echo "    Removing temporary root appsettings.json"
  rm "$REPO_ROOT/appsettings.json"
fi

# ---------------------------------------------------------------------------
# Step 4: Build API container
# ---------------------------------------------------------------------------
if [[ "${SKIP_API:-false}" != "true" ]]; then
  echo "==> Building API container"
  PUBLISH_ARGS=(
    -c Release
    -p:PublishProfile=DefaultContainer
    -p:ContainerRepository="$IMAGE_REPOSITORY/nocturne-api"
    -p:ContainerImageTag="$VERSION"
  )
  if [[ -n "$PUSH" ]]; then
    # Push to remote registry
    PUBLISH_ARGS+=(-p:ContainerRegistry="$REGISTRY")
  fi
  # Without ContainerRegistry, .NET SDK publishes to the local Docker daemon
  dotnet publish src/API/Nocturne.API/Nocturne.API.csproj "${PUBLISH_ARGS[@]}"
  echo "    Tagged: $IMAGE_REPOSITORY/nocturne-api:$VERSION"
else
  echo "==> Skipping API container (SKIP_API=true)"
fi

# ---------------------------------------------------------------------------
# Step 5: Build Web container
# ---------------------------------------------------------------------------
if [[ "${SKIP_WEB:-false}" != "true" ]]; then
  echo "==> Building Web container"
  DOCKER_ARGS=(
    --platform linux/amd64
    --tag "$REGISTRY/$IMAGE_REPOSITORY/nocturne-web:$VERSION"
    --file "$REPO_ROOT/Dockerfile.web"
  )
  if [[ -n "$PUSH" ]]; then
    DOCKER_ARGS+=("$PUSH")
  else
    DOCKER_ARGS+=(--load)
  fi
  docker buildx build "${DOCKER_ARGS[@]}" "$REPO_ROOT"
  echo "    Tagged: $REGISTRY/$IMAGE_REPOSITORY/nocturne-web:$VERSION"
else
  echo "==> Skipping Web container (SKIP_WEB=true)"
fi

echo ""
echo "==> Build complete!"
echo "    nocturne-api:$VERSION"
echo "    nocturne-web:$VERSION"
