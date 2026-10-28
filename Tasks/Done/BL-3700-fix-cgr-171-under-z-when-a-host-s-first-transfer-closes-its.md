---
id: BL-3700
title: Fix CGR-171: Under -Z, when a host's first transfer closes its connection (Connection: close or a -f error), PORT runs the waiting transfers one at a...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr171Tests.cs, Curl.Console/ParallelHostQueue.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitLibrary/ConnectionCache.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/Cgr171Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3700 — Fix CGR-171: Under -Z, when a host's first transfer closes its connection (Connection: close or a -f error), PORT runs the waiting transfers one at a...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-171 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-171: Under -Z, when a host's first transfer closes its connection (Connection: close or a -f error), PORT runs the waiting transfers one at a time, starting with the last; REF starts them all at once

- **Severity:** Medium (the order of -w lines, bodies and error lines differs, and the transfers no longer run concurrently)
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows x3, Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-3`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600&close=1" "http://127.0.0.1:22000/b?d=100" "http://127.0.0.1:22000/c?d=300"`
- The same command with `-f` and the first URL `a?d=600&s=500`.

Scripts: `v1.ps1` (v_p2_close, v_p2_close_r, v_p1_f500), `linux/vl.sh` (vl_p2_close). The server's per-connection timeline is in `runs\v_p2_close\{ref,port}\meta`.

Commands (run each with REF and with PORT):

```sh
curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600&close=1" "http://127.0.0.1:22000/b?d=100" "http://127.0.0.1:22000/c?d=300"
```

**Expected (real curl)**

- at t=606 ms, REF opens two new connections in the same millisecond and runs b and c concurrently. stdout is `0 0 200\n1 0 200\n2 0 200\n` (`0 22 500\n1 0 200\n2 0 200\n` with -f), and the run ends at about 920 ms.

**Actual (Curl)**

- at t=617 ms PORT opens one connection for c only. b starts on a new connection at t=930, after c has finished. stdout is `0 0 200\n2 0 200\n1 0 200\n` (`0 22 500\n2 0 200\n1 0 200\n` with -f). The run ends at about 1040 ms.

**Suspected location in source**

- `Curl.Console/ParallelHostQueue.cs` ~84-160. `Leave` passes the first waiter as `pending`, `FreeWaiting` moves it behind the others, and `StartReleased` starts the released transfers one after another. ADR-0768 is cited in the source.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr171Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-28 (lane 8): Cause was not `ParallelHostQueue` but the pool: `PoolingConnector.WaitsForMultiplexing`
  made the second released transfer wait on the first one's connection being opened until its response
  arrived. Fix (ADR-0866): a connection's first HTTP/1 session (`ConcurrentTransferLimit` null) records its
  key in `ConnectionCache.KeysKnownNotToMultiplex`, and no transfer waits for multiplexing on such a key.
  Touches widened to `Curl.Networking.UnitLibrary/PoolingConnector.cs`, `ConnectionCache.cs`, its `CLAUDE.md`
  and `Curl.Networking.UnitTests/Cgr171Tests.cs` (no Doing task on origin/work/dark-factory named them); the
  focused test lives there, the fixed library's own test project, so the Console files were left unchanged.
- Measured with a C# loopback server (per-path `d=` delay, `close=1`, `s=`), curl 8.22.0 Schannel (Git ucrt64)
  and Curl's Debug build, three runs each, both identical every run:
  `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:P/a?d=600&close=1" "http://127.0.0.1:P/b?d=100" "http://127.0.0.1:P/c?d=300"`
  -> stdout `0 0 200\n1 0 200\n2 0 200\n`, exit 0, empty stderr; with `-f` and `a?d=600&s=500`
  -> `0 22 500\n1 0 200\n2 0 200\n`, exit 22, empty stderr. Server timeline: both now open b and c on two
  connections within a few ms of a ending (curl sends b's request first, Curl c's, ADR-0768's order; not
  visible in output). Requests are default GETs, unchanged by this fix. No files written (`-o NUL`).
- Unit test `Cgr171Tests.ConnectAsync_AfterTheKeysFirstConnectionHeldAnHttp1Session_...` failed with the
  `FindShareable` check reverted and passes with it. Fast tests: 44346 passed, 0 failed. Measure-CodeQuality
  for Curl.Networking.UnitLibrary: 0 failing members. CI on Linux/macOS: the change and tests are pure
  in-memory and platform-neutral.

## Log

- 2026-10-26: Created from CGR-171 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. Under -Z a host's waiting HTTP/1 transfers open side by side after its first ends, as curl 8.22.0 does (CGR-171)
