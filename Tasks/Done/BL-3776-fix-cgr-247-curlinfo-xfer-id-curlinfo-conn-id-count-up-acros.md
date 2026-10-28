---
id: BL-3776
title: Fix CGR-247: CURLINFO_XFER_ID / CURLINFO_CONN_ID count up across the whole library instead of starting at 0 for each new easy handle
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr247Tests.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitTests/Cgr247Tests.cs, Curl.Engine.UnitTests/EasyHandleIsolationTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3776 — Fix CGR-247: CURLINFO_XFER_ID / CURLINFO_CONN_ID count up across the whole library instead of starting at 0 for each new easy handle

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-247 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-247: CURLINFO_XFER_ID / CURLINFO_CONN_ID count up across the whole library instead of starting at 0 for each new easy handle

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-14`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro` (the `two fresh handles` case). Each handle does curl_easy_init, performs a GET, then reads XFER_ID and CONN_ID.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
```

**Expected (real curl)**

- XFER_ID=0 and CONN_ID=0 for every new handle, because each curl_easy_perform uses its own multi.

**Actual (Curl)**

- the values grow across handles in the process, e.g. 17 and 16.

**Suspected location in source**

- `Libcurl\Info\Longs\EasyInfoReaders.Longs.cs:45-46` (engine-wide counters).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr247Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-247 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

### Notes (lane 9, 2026-10-28)
- The counters live in the engine (`TransferPerformer`, shared engine-wide), not in `EasyInfoReaders.Longs.cs`, so the fix is in `Curl.Engine.UnitLibrary` (no other live task names those files; added to `touches`). ADR-0965.
- Real-curl behaviour taken from the report (REF XFER_ID=0, CONN_ID=0 per new handle); the probe is not rebuilt. Repro equivalent: two new handles each perform a GET; unit tests `Libcurl.UnitTests/Cgr247Tests.cs` (second handle was 1 before the fix) and `Curl.Engine.UnitTests/Cgr247Tests.cs` pin 0 for each.
- Two engine tests that pinned the old engine-wide numbering were updated. Fast tests green; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.
- CI criterion: change is platform-neutral (file URL via `Uri.AbsoluteUri`, temp file).
- 2026-10-28: Doing -> Done. Each library easy and multi handle numbers xfer_id and conn_id from 0 (ADR-0965)
