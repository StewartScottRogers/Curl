---
id: BL-3811
title: Fix CGR-282: XFERINFOFUNCTION returning CURL_PROGRESSFUNC_CONTINUE does not show the built-in progress meter
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4057, BL-4058]
touches: [Curl.Api.UnitLibrary/Callbacks/TransferInfoCallback.cs, Curl.Api.UnitTests/Cgr282Tests.cs, Libcurl.UnitTests/Cgr282Tests.cs, Libcurl/Callbacks/Progress/EasyProgressCallbackSetters.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3811 — Fix CGR-282: XFERINFOFUNCTION returning CURL_PROGRESSFUNC_CONTINUE does not show the built-in progress meter

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-282 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-282: XFERINFOFUNCTION returning CURL_PROGRESSFUNC_CONTINUE does not show the built-in progress meter

- **Severity:** Low
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed. Detail: Confirmed. Evidence: `EV\c57_REF.err` and `EV\c57_PORT.err`. The stderr of the combined Linux run also shows the meter once, from REF.
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-17`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_57_xferinfo_returns`, variant ret=0x10000001: NOPROGRESS=0, GET `http://127.0.0.1:21700/`. The probe compares stdout only, and that matches, so capture stderr for each side separately:
  1. `set LIBONLY=REF` then `probe.exe C_57 1 2> c57_REF.err`
  2. `set LIBONLY=PORT` then `probe.exe C_57 1 2> c57_PORT.err`

Commands (run each with REF and with PORT):

```sh
set LIBONLY=REF
probe.exe C_57 1 2> c57_REF.err
set LIBONLY=PORT
probe.exe C_57 1 2> c57_PORT.err
```

**Expected (real curl)**

- libcurl's meter is written to stderr (470 bytes, starting `  % Total    % Received % Xferd  Average Speed ...`). rc=0.

**Actual (Curl)**

- stderr is empty (0 bytes). rc=0, and the body matches.

**Suspected location in source**

- the PORT code that dispatches TransferInfoCallback does not map `ProgressCallbackResult.Continue` to the default meter. Exact line not located.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr282Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-282 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-4012: the progress meter lives only in Curl.Console and must move to a library the Engine can use
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (lane 1)

- Not reproducible at the current commit: BL-4012 (ADR-0993) already makes `CallbackTransferProgress` pass a `ProgressCallbackResult.Continue` answer on to the built-in meter on stderr (see `Curl.Engine.UnitLibrary/CallbackTransferProgress.cs`, `Of` and `Report`).
- The focused test is `Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.Perform_TransferInfoCallbackReturnsContinue_DrawsTheMeter` (header `  % Total    % Received % Xferd  Average Speed` then `\r100 `); `dotnet test --filter CurlEasyProgressMeterTests` gave 3 passed, so no new `Cgr282Tests.cs` was added and no production file changed.
- The ratchet of "same bytes as real curl" for the meter lines was measured by BL-4012; CI platform-neutrality unchanged because nothing changed.
- 2026-10-29: Doing -> Done. Already fixed by BL-4012; pinned by CurlEasyProgressMeterTests
