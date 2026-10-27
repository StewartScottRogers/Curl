---
id: BL-3676
title: Fix CGR-147: curl_easy_perform on an easy handle that is in a multi handle runs a second transfer instead of failing with CURLE_FAILED_INIT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitTests/Cgr147Tests.cs, Libcurl.UnitTests/Cgr147Tests.cs, Libcurl/Easy/CurlEasy.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Cleanup.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3676 — Fix CGR-147: curl_easy_perform on an easy handle that is in a multi handle runs a second transfer instead of failing with CURLE_FAILED_INIT

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-147 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-147: curl_easy_perform on an easy handle that is in a multi handle runs a second transfer instead of failing with CURLE_FAILED_INIT

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-8`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_04_empty_and_misuse`. Easy handle `e` (`http://127.0.0.1:21700/`) is driven to DONE in multi `m` and left attached. Then the application calls `curl_easy_perform(e)`. Command: `probe.exe C_04 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_04 2
```

**Expected (real curl)**

- `easy_perform-while-in-multi rc=2` (CURLE_FAILED_INIT) and no request; the first connection's log is `[GET /]`.

**Actual (Curl)**

- `rc=0`, and an extra request is sent on the first connection: `[GET /,GET /]`.

**Suspected location in source**

- `Libcurl\Easy\CurlEasy.cs:106-118` and `Curl.Engine.UnitLibrary\ManagedEasyTransfer.Perform.cs:26` (neither checks whether the handle belongs to a multi).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr147Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured REF with a P/Invoke probe against libcurl 8.22.0 (Git for Windows `ucrt64/bin/libcurl-4.dll`) and a recording loopback server answering `200 hello`: `e` driven to done in `m`, left attached, then `curl_easy_perform(e)` gives `easy_perform-while-in-multi rc=2 error=[easy handle already used in multi handle]` and the server log `conn,GET / HTTP/1.1` (one request). PORT before the fix: rc=0 and a second `GET /` (the report's Actual; `Libcurl.UnitTests/Cgr147Tests.cs` failed with rc Ok). PORT after the fix, the same probe over `CurlGlobal.Init().MultiInit()/EasyInit()`: `rc=2 error=[easy handle already used in multi handle]`, log `conn,GET / HTTP/1.1` - equal to REF. No stdout, files or other CURLINFO are involved. Linux (curl 8.18.0) also answers 2 per the report; the fix is platform-neutral.
- Fix (ADR-0858): `ManagedEasyTransfer.PerformAsync` takes the multi handle's `MultiCallbacks` marshal as it starts; a perform of a handle in `MultiOwners` without one is the application's call and answers `FailedInit` with curl's error text. `CurlEasy.cs` needed no change: it already fills the error buffer from `ErrorText`. The ADR-0850 recursive-call check still comes first, so the CGR-146 variants still match (Cgr146Tests pass).
- Added `Curl.Engine.UnitTests/ManagedEasyTransferTests.Cleanup.cs` to touches (no task in Doing on origin/work/dark-factory names it): its `DisposeAsync_HandleInAMultiHandle_...` test performed a handle directly while it was in a multi handle, the call now refused; it drives the multi handle instead.
- Focused tests: `Curl.Engine.UnitTests/Cgr147Tests.cs` (refused, error text, one request; removed handle performs again) and `Libcurl.UnitTests/Cgr147Tests.cs`; the refusal tests failed with the fix reverted and pass with it. Build clean, fast tests green; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.
- Pipeline `feature` run directly: a one-check fix in one method, no design beyond the ADR.
## Log

- 2026-10-26: Created from CGR-147 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. curl_easy_perform of a handle still in a multi handle answers CURLE_FAILED_INIT with curl's error text and sends nothing, as libcurl 8.22.0
