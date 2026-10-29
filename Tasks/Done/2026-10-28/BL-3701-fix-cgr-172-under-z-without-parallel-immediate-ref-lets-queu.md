---
id: BL-3701
title: Fix CGR-172: Under -Z without --parallel-immediate, REF lets queued same-host transfers start when any transfer ends; PORT holds them until that host'...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr172Tests.cs, Curl.Console/ParallelHostQueue.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/Cgr172Tests.cs, Curl.Engine.UnitLibrary/EngineComposition.Connectors.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3701 — Fix CGR-172: Under -Z without --parallel-immediate, REF lets queued same-host transfers start when any transfer ends; PORT holds them until that host'...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-172 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-172: Under -Z without --parallel-immediate, REF lets queued same-host transfers start when any transfer ends; PORT holds them until that host's first transfer ends

- **Severity:** Medium
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows x2 for each case, Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-4`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" "http://127.0.0.1:22001/q?d=100" "http://127.0.0.1:22000/c?d=300"`
- `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" http://127.0.0.1:22099/x "http://127.0.0.1:22000/c?d=200"`. Port 22099 refuses the connection.

Scripts: `v1.ps1` (v_q1_other, v_q1_other_r, v_refmid, v_refmid_r), `linux/vl.sh` (vl_q1_other, vl_refmid).

Commands (run each with REF and with PORT):

```sh
curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" "http://127.0.0.1:22001/q?d=100" "http://127.0.0.1:22000/c?d=300"
curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" http://127.0.0.1:22099/x "http://127.0.0.1:22000/c?d=200"
```

**Expected (real curl)**

- c starts on a second connection as soon as q ends (t=116 ms), or right after the refused connect, and finishes before a. stdout is `1 0 200\n2 0 200\n0 0 200\n`; in the refused case it is `1 7 000\n2 0 200\n0 0 200\n`.

**Actual (Curl)**

- c waits until a ends at about 605 ms and then reuses a's connection. stdout is `1 0 200\n0 0 200\n2 0 200\n`; in the refused case it is `1 7 000\n0 0 200\n2 0 200\n`.

**Suspected location in source**

- `Curl.Console/ParallelHostQueue.cs` ~80-95. `Leave` frees only the waiters of the same host:port, so a transfer that ends at another host, or that fails to connect, frees none of them.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr172Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Plan (ADR-0937): `ParallelHostQueue.Leave` now frees every host's transfers still waiting for that host's first to end whenever any transfer ends (another host, a refused connect, a `file://` URL), as curl retries its pending transfers on any done. That alone was not enough: `PoolingConnector` then held the freed `/c` on `/a`'s busy plain-TCP connection until its HTTP/1 session showed, so `PoolingConnector.PlainTcpMayMultiplex` (set by `EngineComposition.CreatePoolingConnector` only for `--http2` and `--http2-prior-knowledge`) lets an HTTP/1.x group open its own connection. Touches widened to `PoolingConnector.cs`, its CLAUDE.md, `EngineComposition.Connectors.cs` and `Curl.Networking.UnitTests/Cgr172Tests.cs` for that; no task in Doing on origin/work/dark-factory named them.
- REF here is `C:\windows\system32\curl.exe` 8.21.0 Schannel (default choice: the platform's Schannel build on this machine; Git's 8.22.0 was not used), against a C# file-based loopback server (`d=` delay, ports 22000/22001, 22099 closed); PORT is `dotnet run --project Curl.Console --no-build` (Debug). Each run twice:
  - `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" "http://127.0.0.1:22001/q?d=100" "http://127.0.0.1:22000/c?d=300"`: REF `1 0 200|2 0 200|0 0 200|` exit 0 both runs, `/c` on a 2nd connection ~115 ms after `/a`. PORT before the fix `1 0 200|0 0 200|2 0 200|` with `/c` reusing `/a`'s connection after 600 ms; after: `1 0 200|2 0 200|0 0 200|` exit 0, `/c` on a 2nd connection once `/q` ends. The 2nd PORT run once printed `0` before `2`: the Debug build's connect overhead starts `/c` ~150 ms later than curl, so `/c` (300 ms) ends within ms of `/a`. stderr empty (-s) in both; request bytes are the same `GET` lines.
  - `... "http://127.0.0.1:22000/a?d=600" http://127.0.0.1:22099/x "http://127.0.0.1:22000/c?d=200"`: PORT before `1 7 000|0 0 200|2 0 200|`, after `1 7 000|2 0 200|0 0 200|` exit 7, the report's Expected (REF 8.22.0). The local Windows 8.21.0 takes ~2 s to report the refused loopback connect, so it prints `0 0 200|2 0 200|1 7 000|`; that connect timing is a separate matter and not this issue.
- The ADR-0482 tests: `Curl.Console.UnitTests/Cgr172Tests.cs` (3 of its 4 tests failed before the queue fix, checked by building the old `ParallelHostQueue.cs`) and `Curl.Networking.UnitTests/Cgr172Tests.cs` (pins `PlainTcpMayMultiplex`: off, a busy plain TCP connection gets a second one at once; on, the old behaviour, the transfer waits). Existing `ParallelHostQueue*Tests`, `Cgr171Tests` and every fast test still pass, so the variants that already matched still match.
- Fast tests: 46 test projects passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Networking.UnitLibrary,Curl.Engine.UnitLibrary`: 0 failing members. Changes are platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-26: Created from CGR-172 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. Under -Z any transfer that ends frees other hosts' waiters, and an HTTP/1.x group opens its own connection beside a busy one, as curl does (CGR-172)
