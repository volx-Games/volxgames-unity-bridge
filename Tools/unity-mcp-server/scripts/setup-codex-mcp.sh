#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SERVER_PATH="$REPO_ROOT/Tools/unity-mcp-server/server.js"
SERVER_NAME="${1:-unity-bridge}"

if ! command -v codex >/dev/null 2>&1; then
  echo "codex CLI not found in PATH." >&2
  exit 1
fi

echo "Adding MCP server '$SERVER_NAME' to Codex..."
if [[ -n "${UNITY_BRIDGE_URL:-}" ]]; then
  codex mcp add "$SERVER_NAME" --env "UNITY_BRIDGE_URL=$UNITY_BRIDGE_URL" -- node "$SERVER_PATH"
elif [[ -n "${UNITY_BRIDGE_PROJECT_PATH:-}" ]]; then
  codex mcp add "$SERVER_NAME" --env "UNITY_BRIDGE_PROJECT_PATH=$UNITY_BRIDGE_PROJECT_PATH" -- node "$SERVER_PATH"
else
  codex mcp add "$SERVER_NAME" -- node "$SERVER_PATH"
fi

echo
echo "Done. Verify with:"
echo "  codex mcp list"
