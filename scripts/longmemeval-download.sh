#!/usr/bin/env bash
# Downloads LongMemEval (MIT, (c) 2024 Di Wu, https://github.com/xiaowu0162/LongMemEval) into evals/longmemeval/data/,
# which git ignores: the benchmark's files are never committed. Checks them against evals/longmemeval/SHA256SUMS.
# Usage: scripts/longmemeval-download.sh [oracle|s|m ...]   (default: oracle s)
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
base="https://huggingface.co/datasets/xiaowu0162/longmemeval-cleaned/resolve/main"
dir="evals/longmemeval/data"
mkdir -p "$dir"
variants=("$@"); [ ${#variants[@]} -gt 0 ] || variants=(oracle s)
for v in "${variants[@]}"; do
  case "$v" in
    oracle) file="longmemeval_oracle.json" ;;
    s) file="longmemeval_s_cleaned.json" ;;
    m) file="longmemeval_m_cleaned.json" ;;
    *) echo "unknown variant: $v (oracle, s or m)" >&2; exit 2 ;;
  esac
  if [ ! -f "$dir/$file" ]; then
    echo "downloading $file"
    curl -fL --retry 3 -o "$dir/$file.part" "$base/$file"
    mv "$dir/$file.part" "$dir/$file"
  fi
  expected=$(grep " $file\$" evals/longmemeval/SHA256SUMS | cut -d' ' -f1 || true)
  actual=$(sha256sum "$dir/$file" | cut -d' ' -f1)
  if [ -z "$expected" ]; then
    echo "$file: no pinned hash yet; pin it with: echo \"$actual  $file\" >> evals/longmemeval/SHA256SUMS" >&2
  elif [ "$expected" != "$actual" ]; then
    echo "$file: hash $actual differs from the pinned $expected" >&2; exit 1
  else
    echo "$file: ok"
  fi
done
