#!/bin/zsh
# metavr.sh : prints the path of the metavr CLI (on PATH, or the copy the meta-vr plugin installs)
if command -v metavr >/dev/null 2>&1; then command -v metavr; exit 0; fi
M=$(ls -d ~/.metavr/npm/binaries/*/*/metavr 2>/dev/null | sort -V | tail -1)
[ -n "$M" ] && { echo "$M"; exit 0; }
echo "metavr not found. Install it with 'npx -y metavr' or the meta-vr plugin." >&2
exit 1
