---
id: BL-3678
title: Fix CGR-149: Connections are not closed by curl_multi_cleanup or curl_easy_cleanup
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr149Tests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.MaxConnects.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3678 — Fix CGR-149: Connections are not closed by curl_multi_cleanup or curl_easy_cleanup

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-149 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-149: Connections are not closed by curl_multi_cleanup or curl_easy_cleanup

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-10`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- command: `probe.exe C_60,C_61 2`.
  - `C_60_conn_after_multi_cleanup`: one keep-alive transfer in a multi, then remove_handle, multi_cleanup (wait 500 ms) and easy_cleanup (wait 500 ms). After each step the server counts the connections still open.
  - `C_61_cross_multi_reuse`: two multis, one after the other, each with a new easy handle.

Commands (run each with REF and with PORT):

```sh
probe.exe C_60,C_61 2
```

**Expected (real curl)**

- C_60: `open after multi_cleanup=0`, `open after easy_cleanup=0`. C_61: `openAtEnd=0`.

**Actual (Curl)**

- C_60: `open after multi_cleanup=1`, `open after easy_cleanup=1`. C_61: `openAtEnd=2`. Connections are left open at the end of nearly every multi case (`openAtEnd>=1`).

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:289-308` (`DisposeAsync` does not close pooled connections).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr149Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause: `ManagedMultiTransfer.DisposeAsync` (`curl_multi_cleanup`) cancelled and awaited the performs but left the keep-alive connections in the multi handle's own pool (BL-3271) open until the engine itself was disposed. Fix: once every perform has ended it closes the handle's idle connections through its `closeIdleConnections` delegate (`ConnectionCache.CloseIdleAsync` of the multi's pool), as libcurl 8.22.0 closes its pool. A share handle's pool is not the multi's, so connections a share holds stay open, as libcurl's do. No ADR: this only makes the engine match measured libcurl.
- Measured (2026-10-28) with C# file-based probes outside the repository and a loopback server answering keep-alive `200 hello` that counts the connections the client has not closed; one transfer per multi, perform and poll until done, remove_handle, 500 ms waits. REF: Git for Windows ucrt64 `libcurl-4.dll` 8.22.0 through P/Invoke; PORT: the `Libcurl` C# API (`CurlGlobal.Init`, `MultiInit`, `CleanupAsync`, `CurlEasy.DisposeAsync`) at this commit.
  - `C_60`: REF `open after remove_handle=1`, `open after multi_cleanup=0`, `open after easy_cleanup=0`; PORT the same (the report gives PORT 1 and 1 before the fix).
  - `C_61` (two multis one after the other, each with a new easy handle): REF `openAtEnd=0`, PORT `openAtEnd=0` (the report gives 2 before the fix).
  - Every call returned 0 on both sides; the request bytes are not changed by this fix, which only adds a close at cleanup. `open after remove_handle=1` matched before and still matches.
- Unit tests: `Cgr149Tests` (both cases over the engine with an in-memory connector) fail 2/2 with the new close line commented out and pass with it. `ManagedMultiTransferTests.MaxConnects.cs` disposed the multi only to wait for the MaxConnects closes and then read which connections were closed; it now reads that before cleanup (the closes complete synchronously over `QueuedConnection`) and checks that cleanup closes the rest, so it was added to `touches` (no task in `origin/work/dark-factory`'s Tasks/Doing names it).
- Linux and macOS: the change and tests are platform-neutral (no path, OS error or certificate), so CI on them is covered by the green fast run here.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-149 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. curl_multi_cleanup closes the multi handle's idle connections, so none stays open after multi or easy cleanup (CGR-149)
