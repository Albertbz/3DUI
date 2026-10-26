#!/bin/zsh
# wait_log.sh [timeout-seconds] : waits for a coroutine test (grab.cs, gallery_interact.cs, video.cs)
# to finish, then prints its log.
U=~/.unity/bin/unity
limit=${1:-180}
for i in $(seq 1 $((limit / 3))); do
  "$U" command script eval --code 'return System.AppDomain.CurrentDomain.GetData("xrsim.done") != null;' --no-banner 2>/dev/null | grep -q '"result":true' && break
  sleep 3
done
"$U" command script eval --code 'return System.AppDomain.CurrentDomain.GetData("xrsim.log")?.ToString() ?? "(no log)";' --no-banner --format json 2>&1 | python3 -I -c '
import json, sys
d, _ = json.JSONDecoder().raw_decode(sys.stdin.read().lstrip())
r = d["data"]; r = r[0] if isinstance(r, list) else r
print(r["result"]["result"] if d["success"] else d["errors"])'
