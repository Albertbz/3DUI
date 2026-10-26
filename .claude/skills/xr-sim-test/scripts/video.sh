#!/bin/zsh
# video.sh : renders the gallery sequence (video.cs) and encodes it to $XRSIM_OUT/gallery.mp4,
# plus a 5-frame contact sheet (gallery_strip.png) that can be viewed as an image.
DIR="${0:A:h}"
OUT="${XRSIM_OUT:-${TMPDIR:-/tmp}/xrsim-test}"; OUT="${OUT%/}"
"$DIR/ev.sh" "$DIR/video.cs" && "$DIR/wait_log.sh" 300
command -v ffmpeg >/dev/null || { echo "ffmpeg not installed; frames are in $OUT/video"; exit 0; }
ffmpeg -loglevel error -y -framerate 30 -i "$OUT/video/f%04d.png" -c:v libx264 -pix_fmt yuv420p -crf 20 "$OUT/gallery.mp4"
n=$(ls "$OUT/video" | wc -l | tr -d ' ')
ffmpeg -loglevel error -y -i "$OUT/gallery.mp4" -vf "select='eq(n\,5)+eq(n\,$((n * 15 / 100)))+eq(n\,$((n * 40 / 100)))+eq(n\,$((n * 60 / 100)))+eq(n\,$((n - 5)))',scale=420:-1,tile=5x1" -frames:v 1 "$OUT/gallery_strip.png"
echo "$OUT/gallery.mp4"
echo "$OUT/gallery_strip.png"
