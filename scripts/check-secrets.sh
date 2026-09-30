#!/usr/bin/env bash
# Fails when a tracked file looks like it holds a secret: a private key, an API key or token, a JWT, or a connection
# string with a password. Generic patterns only; run by CI and before every push.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

patterns=(
  '-----BEGIN [A-Z ]*PRIVATE KEY-----'
  'sk-(proj-|or-v1-|ant-)?[A-Za-z0-9_-]{20,}'
  'gh[pousr]_[A-Za-z0-9]{30,}'
  'github_pat_[A-Za-z0-9_]{40,}'
  'AKIA[0-9A-Z]{16}'
  'AIza[0-9A-Za-z_-]{35}'
  'xox[baprs]-[A-Za-z0-9-]{10,}'
  'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'
  '(Password|Pwd)=[^;"'"'"'[:space:]<>{}$]{4,}'
)

found=0
for pattern in "${patterns[@]}"; do
  if git grep -nIE -e "$pattern" -- ':!scripts/check-secrets.sh'; then
    found=1
  fi
done

if [ "$found" -ne 0 ]; then
  echo "Possible secrets found above: remove them (and rotate them if they were real)." >&2
  exit 1
fi
echo "No secrets found."
