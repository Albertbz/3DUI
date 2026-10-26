#!/bin/zsh
# photos.sh baseline : records which photo files already exist (run before testing)
# photos.sh cleanup  : deletes only photos created since the baseline (the user's own photos are kept)
OUT="${XRSIM_OUT:-${TMPDIR:-/tmp}/xrsim-test}"; OUT="${OUT%/}"; mkdir -p "$OUT"
PHOTOS="$HOME/Library/Application Support/$(sed -n 's/^  companyName: //p' "${0:A:h}/../../../../ProjectSettings/ProjectSettings.asset")/$(sed -n 's/^  productName: //p' "${0:A:h}/../../../../ProjectSettings/ProjectSettings.asset")/Photos"
case "$1" in
  baseline)
    mkdir -p "$PHOTOS"; ls "$PHOTOS" > "$OUT/photo_baseline.txt"
    echo "baseline: $(wc -l < "$OUT/photo_baseline.txt" | tr -d ' ') files in $PHOTOS" ;;
  cleanup)
    [ -f "$OUT/photo_baseline.txt" ] || { echo "no baseline recorded; not deleting anything"; exit 1; }
    n=0
    for f in "$PHOTOS"/*(N); do
      grep -qxF "${f:t}" "$OUT/photo_baseline.txt" || { rm -f "$f"; n=$((n + 1)); }
    done
    echo "deleted $n test files from $PHOTOS" ;;
  *) echo "usage: photos.sh baseline|cleanup"; exit 2 ;;
esac
