#!/usr/bin/env bash
# Starts filum-mcp on a temporary empty folder, sends the MCP handshake and a tools/list over stdio, and fails unless
# the server answers both with the engine's tools and prints nothing but protocol on stdout.
# Usage: scripts/smoke-mcp.sh <command that starts filum-mcp...>   e.g. scripts/smoke-mcp.sh ./filum-mcp-linux-x64
set -euo pipefail
[ "$#" -ge 1 ] || { echo "usage: $0 <filum-mcp command...>" >&2; exit 2; }

home="$(mktemp -d)"
out="$home.out"
trap 'rm -rf "$home" "$out"' EXIT

{
  printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1"}}}'
  printf '%s\n' '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  printf '%s\n' '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
  sleep 10
} | FILUM_HOME="$home/memory" timeout 60 "$@" > "$out" || true

grep -q '"serverInfo":{"name":"filum"' "$out" || { echo "no initialize answer from filum-mcp" >&2; cat "$out" >&2; exit 1; }
grep -q '"name":"memory_overview"' "$out" || { echo "tools/list did not return the engine's tools" >&2; exit 1; }
if grep -qv '^{' "$out"; then echo "stdout carried something other than protocol messages" >&2; exit 1; fi
[ -f "$home/memory/filum.md" ] || [ -d "$home/memory/.filum" ] || { echo "the memory folder was not created" >&2; exit 1; }
echo "filum-mcp answered the handshake and listed $(grep -o '"name":"[a-z_]*"' "$out" | grep -c -E '"name":"(memory|collection|skill|events|facts?|proposal)_') tools."
