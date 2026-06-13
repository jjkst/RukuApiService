#!/usr/bin/env bash
# Runs RukuServiceApi.IntegrationTests against a fully isolated Docker stack.
# Requires: Docker, dotnet SDK 8
set -euo pipefail

COMPOSE_FILE="docker-compose.test.yml"
API_URL="http://localhost:5002/health/live"
MAX_WAIT=120

cleanup() {
  echo ""
  echo "Tearing down test environment..."
  docker compose -f "$COMPOSE_FILE" down --remove-orphans
}
trap cleanup EXIT

echo "Building and starting test environment..."
docker compose -f "$COMPOSE_FILE" up --build -d

echo "Waiting for API to be ready (max ${MAX_WAIT}s)..."
elapsed=0
until curl -sf "$API_URL" > /dev/null 2>&1; do
  if [ "$elapsed" -ge "$MAX_WAIT" ]; then
    echo "ERROR: API did not become healthy after ${MAX_WAIT}s."
    echo ""
    echo "--- api-test logs ---"
    docker compose -f "$COMPOSE_FILE" logs api-test
    exit 1
  fi
  sleep 3
  elapsed=$((elapsed + 3))
  printf "  ...%ds\n" "$elapsed"
done
echo "API is ready."

echo ""
echo "Running integration tests..."
dotnet test RukuServiceApi.IntegrationTests \
  --logger "console;verbosity=normal" \
  --results-directory "RukuServiceApi.IntegrationTests/TestResults"
