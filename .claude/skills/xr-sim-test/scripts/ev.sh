#!/bin/zsh
# ev.sh <file.cs> [arg1 .. arg5]
# Runs a C# snippet inside the connected Unity Editor and prints its return value.
# Placeholders in the snippet: __OUT__ (output folder) and __ARG1__ .. __ARG5__.
# Output folder: $XRSIM_OUT, default ${TMPDIR:-/tmp}/xrsim-test.
set -e
OUT="${XRSIM_OUT:-${TMPDIR:-/tmp}/xrsim-test}"
OUT="${OUT%/}"
mkdir -p "$OUT"
SRC="$1"; shift
TMP="$OUT/_run.cs"
sed -e "s|__OUT__|$OUT|g" -e "s|__ARG1__|${1:-}|g" -e "s|__ARG2__|${2:-}|g" \
    -e "s|__ARG3__|${3:-}|g" -e "s|__ARG4__|${4:-}|g" -e "s|__ARG5__|${5:-}|g" "$SRC" > "$TMP"
~/.unity/bin/unity command script eval_file --file "$TMP" --no-banner --format json 2>&1 | python3 -I -c '
import json, sys
raw = sys.stdin.read()
try:
    d, _ = json.JSONDecoder().raw_decode(raw.lstrip())  # the CLI may append a second JSON document
except Exception:
    print(raw[:2000]); sys.exit(1)
if not d.get("success"):
    print("ERROR:", d.get("errors")); sys.exit(1)
r = d["data"]; r = r[0] if isinstance(r, list) else r
res = r.get("result", r)
print(res.get("result") if isinstance(res, dict) and "result" in res else res)'
