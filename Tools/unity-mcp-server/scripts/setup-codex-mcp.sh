#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SERVER_PATH="$REPO_ROOT/Tools/unity-mcp-server/server.js"
BRIDGE_URL="${UNITY_BRIDGE_URL:-http://127.0.0.1:48761}"
SERVER_NAME="${1:-unity-bridge}"

if ! command -v codex >/dev/null 2>&1; then
  echo "codex CLI not found in PATH." >&2
  exit 1
fi

echo "Adding MCP server '$SERVER_NAME' to Codex..."
codex mcp add "$SERVER_NAME" --env "UNITY_BRIDGE_URL=$BRIDGE_URL" -- node "$SERVER_PATH"

echo
echo "Done. Verify with:"
echo "  codex mcp list"
