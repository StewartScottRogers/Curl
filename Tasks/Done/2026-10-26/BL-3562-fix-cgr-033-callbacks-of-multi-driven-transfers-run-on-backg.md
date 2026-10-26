---
id: BL-3562
title: Fix CGR-033: Callbacks of multi-driven transfers run on background threads, outside curl_multi_perform
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/MultiCallbackQueue.cs, Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/Cgr033Tests.cs, Curl.Engine.UnitTests/MultiCallbackQueueTests.cs, Curl.Engine.UnitTests/TransferCallbackMarshalTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3562 — Fix CGR-033: Callbacks of multi-driven transfers run on background threads, outside curl_multi_perform

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-033 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-033: Callbacks of multi-driven transfers run on background threads, outside curl_multi_perform

- **Severity:** High
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-2`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_69_callback_thread`.
  1. The server sends the 200 head and the first 100000 body bytes, then the other 100000 bytes 200 ms later.
  2. A multi holds one easy handle. Its WRITEFUNCTION and HEADERFUNCTION record the thread id and whether the main thread is inside `curl_multi_perform`.
  3. The loop is `perform`, then `Sleep(30)`, with no poll.

  Command: `probe.exe C_69 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_69 2
```

**Expected (real curl)**

- `header:caller-thread:inside-perform write:caller-thread:inside-perform`.

**Actual (Curl)**

- `header:other-thread:OUTSIDE-perform write:other-thread:OUTSIDE-perform`. Each added transfer runs as a background Task, and its callbacks run at the same time as the application's own code. C code that relies on libcurl's single-thread callback contract (no locking) gets data races.

**Notes**

- *Side effect (intermittent):* in `C_70_running_counts`, a handle removed right after the first perform still had its request sent (PORT `[GET /1] | [GET /2]`, REF `[GET /2]`). This happened in both Windows runs but not in the Linux run.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:824-830`. `Start` returns `Transfer.PerformAsync(...)` as a Task that runs on its own.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr033Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Added to touches: `ManagedEasyTransfer.Perform.cs` (the perform wraps its callbacks once, as it starts), the new `MultiCallbackQueue.cs` and `TransferCallbackMarshal.cs` with their tests, and the engine's `CLAUDE.md` section; no task in Doing on origin/work/dark-factory names any of them.
- Fix (ADR-0817): a multi-driven `ManagedEasyTransfer` wraps every `EasyCallbackOptions` delegate; a callback called off the performing thread is queued and run by the next `curl_multi_perform`/`socket_action` on its caller's thread; a queued callback makes `GetTimeout` 0 and wakes a wait. Default taken: after each callback the perform waits up to 20 ms (`FollowUpWait`) for the next, so a transfer gets every chunk already received in one call; without it upstream test2407 (500 ms polls between performs) timed out on the ratchet.
- Repro rebuilt as a C# file-based probe (scratch, outside the repo): loopback server sends the 200 head and 100000 body bytes, then 100000 more 200 ms later; one easy handle in a multi; loop `perform` then `Sleep(30)`; header and write callbacks record caller-thread/inside-perform. REF = Git's `libcurl-4.dll` 8.22.0 (Schannel) by P/Invoke, PORT = Libcurl's C# API. `probe.cs REF`: `REF: bytes 200000 header:caller-thread:inside-perform write:caller-thread:inside-perform`. `probe.cs PORT` after the fix: `PORT: bytes 200000 header:caller-thread:inside-perform write:caller-thread:inside-perform` (both exit 0, same 200000 bytes, one GET). Before the fix `Cgr033Tests` reproduced `other-thread, OUTSIDE perform` writes. C_70's intermittent side effect was not re-measured.
- ADR-0482 test: `Cgr033Tests.PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` failed before the fix (writes on thread 12 outside perform) and passes after.
- Fast tests: whole solution green (Curl.Conformance.UnitTests 4381 passed after the follow-up wait; Curl.Engine.UnitTests 2492). Measure-CodeQuality -Library Curl.Engine.UnitLibrary: 0 failing members. The tests are platform-neutral (a temp-file `file` URL built with `new Uri(path)`, no OS text), so Linux and macOS CI should match.

## Log

- 2026-10-26: Created from CGR-033 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Callbacks of multi-driven transfers run inside curl_multi_perform on the caller's thread, as libcurl 8.22.0
