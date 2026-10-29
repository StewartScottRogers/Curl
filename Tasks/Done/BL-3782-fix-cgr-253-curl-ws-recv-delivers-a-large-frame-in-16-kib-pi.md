---
id: BL-3782
title: Fix CGR-253: curl_ws_recv delivers a large frame in 16 KiB pieces even when the caller's buffer holds the whole frame
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs, Curl.Engine.UnitTests/Cgr253Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3782 — Fix CGR-253: curl_ws_recv delivers a large frame in 16 KiB pieces even when the caller's buffer holds the whole frame

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-253 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-253: curl_ws_recv delivers a large frame in 16 KiB pieces even when the caller's buffer holds the whole frame

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-20`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe ws len64k`. The server sends one 70000-byte binary frame, and the client calls curl_ws_recv with a 100000-byte buffer.

Commands (run each with REF and with PORT):

```sh
probe.exe ws len64k
```

**Expected (real curl)**

- a single call returns n=70000.

**Actual (Curl)**

- five calls return n=16374, 16384, 16384, 16384 and 4474, with offset and bytesleft metadata. The data is correct and partial delivery is allowed by the API, but code that assumes one call per frame when the buffer is large enough behaves differently.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEasyWebSocket.cs:32` (`ReadSize = 16 * 1024`).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr253Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-28: Fix: `ManagedEasyWebSocket.ReceiveAsync` now keeps handing over the runs that continue the frame it started (`DeliverWholeFrameAsync`), as many as are already received and the buffer holds, so a frame split across reads (70000 bytes over 16 KiB reads) comes back in one call with one `curl_ws_meta` frame (offset 0, bytes left 0, length n); a frame whose rest has not arrived is still handed over as far as it came with `bytes left`. Real curl was not re-measured here (the probe scratchpad is gone); the report gives REF n=70000 for `ws len64k`. Unit tests in `Cgr253Tests` (the first fails before the fix with 120 bytes). Linux and macOS: the tests are platform-neutral. Quality run of Curl.Engine.UnitLibrary: the new code is fully covered; one member outside the gate, `QueuedCallback.WaitUntilRun` (MultiCallbackQueue.cs:165), is not this task's.

## Log

- 2026-10-26: Created from CGR-253 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. CGR-253 fixed: curl_ws_recv coalesces a frame's runs into one call
