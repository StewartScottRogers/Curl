#!/usr/bin/env bash
# Renders Curl's history across every branch to gource.mp4 (about 60 s at 3840x2160, 4K)
# and gource.gif (the largest size that stays under GitHub's 10 MB inline limit) in the
# directory given. Each file is written under a temporary name and moved into place only
# once it is complete. Runs on the GitHub runner (under xvfb) and locally in Git Bash.
set -euo pipefail

out="${1:?usage: render.sh <output directory>}"
mkdir -p "$out"
here="$(cd "$(dirname "$0")" && pwd)"
py="$(command -v python3 || command -v python)"
log="$out/gource.log"

width=3840
height=2160
# Gource sizes text and avatars in pixels: scale them with the frame so 4K reads like 720p.
scale=$(( width / 1280 ))

"$py" "$here/make-log.py" > "$log"
first=$(head -n 1 "$log" | cut -d'|' -f1)
last=$(tail -n 1 "$log" | cut -d'|' -f1)
days=$(( (last - first) / 86400 + 1 ))
# About 55 s of history, however long the project gets: 0.2 to 30 seconds per day.
spd=$(awk -v d="$days" 'BEGIN { s = 55 / d; if (s > 30) s = 30; if (s < 0.2) s = 0.2; printf "%.2f", s }')

run=()
if command -v xvfb-run > /dev/null; then run=(xvfb-run -a -s "-screen 0 ${width}x${height}x24"); fi

"${run[@]}" gource "$log" --log-format custom -"${width}x${height}" \
    --title "Curl - a drop-in replacement for curl, in C# on .NET 10" \
    --seconds-per-day "$spd" --auto-skip-seconds 0.5 --max-file-lag 0.1 \
    --file-idle-time 0 --key --highlight-users --hide mouse,progress \
    --date-format "%Y-%m-%d" --background-colour 0d1117 \
    --font-size 18 --font-scale "$scale" --user-scale "$scale" \
    --stop-at-end --output-framerate 30 --output-ppm-stream - \
  | ffmpeg -y -loglevel error -r 30 -f image2pipe -vcodec ppm -i - \
      -c:v libx264 -preset slow -crf 16 -pix_fmt yuv420p -movflags +faststart \
      "$out/gource.master.mp4"

# GitHub refuses a file over 100 MB: step the quality down only if the render needs it.
cp -f "$out/gource.master.mp4" "$out/gource.tmp.mp4"
for crf in 20 24 28; do
    if [ "$(wc -c < "$out/gource.tmp.mp4")" -le $(( 95 * 1024 * 1024 )) ]; then break; fi
    ffmpeg -y -loglevel error -i "$out/gource.master.mp4" -c:v libx264 -preset slow \
        -crf "$crf" -pix_fmt yuv420p -movflags +faststart "$out/gource.tmp.mp4"
done
mv -f "$out/gource.tmp.mp4" "$out/gource.mp4"
rm -f "$out/gource.master.mp4"

# GitHub only plays a GIF inline, and shows nothing over 10 MB: the largest that fits.
limit=$(( 9500 * 1024 ))
for spec in "1280 12 0.5" "1024 12 0.5" "800 12 0.5" "640 10 0.5" "640 10 0.33" "480 8 0.33"; do
    read -r gw fps speed <<< "$spec"
    ffmpeg -y -loglevel error -i "$out/gource.mp4" -vf \
        "setpts=${speed}*PTS,fps=${fps},scale=${gw}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=5" \
        -loop 0 "$out/gource.tmp.gif"
    if [ "$(wc -c < "$out/gource.tmp.gif")" -le "$limit" ]; then break; fi
done
mv -f "$out/gource.tmp.gif" "$out/gource.gif"
rm -f "$log"
echo "rendered $days day(s) at ${spd}s/day, ${width}x${height}: mp4 $(wc -c < "$out/gource.mp4") bytes, gif ${gw}px $(wc -c < "$out/gource.gif") bytes"
