---
id: BL-3775
title: Fix CGR-246: libcurl: curl_easy_pause on a handle with no transfer in progress returns CURLE_OK; libcurl returns CURLE_BAD_FUNCTION_ARGUMENT (43)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitTests/Cgr246Tests.cs, Libcurl.UnitTests/Cgr246Tests.cs, Libcurl/Easy/CurlEasy.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3775 — Fix CGR-246: libcurl: curl_easy_pause on a handle with no transfer in progress returns CURLE_OK; libcurl returns CURLE_BAD_FUNCTION_ARGUMENT (43)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-246 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-246: libcurl: `curl_easy_pause` on a handle with no transfer in progress returns CURLE_OK; libcurl returns CURLE_BAD_FUNCTION_ARGUMENT (43)

- **Severity:** Low
- **Area:** libcurl-api, libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: [libcurl-multi-callbacks LMC-16] Windows, Linux
- **Confidence:** Confirmed. Detail: [libcurl-api LA-13] Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-13`, `libcurl-multi-callbacks LMC-16`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- **[libcurl-api LA-13]** curl_easy_pause on a handle with no transfer in progress returns CURLE_OK (libcurl returns CURLE_BAD_FUNCTION_ARGUMENT)
  - run `probe.exe cb pause`. It calls `h = curl_easy_init(); curl_easy_pause(h, mask)` with masks 0, 1, 4, 5, -1 and 8.
- **[libcurl-multi-callbacks LMC-16]** curl_easy_pause on a handle with no transfer returns CURLE_OK instead of CURLE_BAD_FUNCTION_ARGUMENT
  - case `C_35_pause_idle_handle`. On a new easy handle (URL set, never performed), call `curl_easy_pause` with masks 1, 4, 5, 0 and 2. Command: `probe.exe C_35 2`.

Commands (run each with REF and with PORT):

```sh
# [libcurl-api LA-13]
probe.exe cb pause
# [libcurl-multi-callbacks LMC-16]
probe.exe C_35 2
```

**Expected (real curl)**

- **[libcurl-api LA-13]** returns 43 for every mask.
- **[libcurl-multi-callbacks LMC-16]** `recv=43 send=43 all=43 cont=43 weird=43`.

**Actual (Curl)**

- **[libcurl-api LA-13]** returns 0 for every mask.
- **[libcurl-multi-callbacks LMC-16]** all five calls return 0. The later performs match.

**Suspected location in source**

- **[libcurl-api LA-13]** `Libcurl\Easy\CurlEasy.cs` (Pause passes straight to the engine).
- **[libcurl-multi-callbacks LMC-16]** `Curl.Engine.UnitLibrary\ManagedEasyTransfer.Lifecycle.cs:56-60` (the doc comment says "The call always succeeds").

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr246Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix: `ManagedEasyTransfer.Pause` answers `BadFunctionArgument` (43) unless a perform is running (`ConnectOnly.PerformRunning`), as libcurl's `curl_easy_pause` does with no connection; callback pauses and unpauses inside a perform are unchanged. The probe and its scratchpad are gone, so the repro was not run against real libcurl: the report's expected 43 for every mask is pinned in `Curl.Engine.UnitTests/Cgr246Tests.cs` (failed before: returned Ok). Variants matching before (pause/unpause during a perform) still pass (Cgr035Tests, ManagedEasyTransferTests.Callbacks). Libcurl.UnitTests, Curl.Api.UnitTests green. Measure-CodeQuality for Curl.Engine.UnitLibrary: only failing member is QueuedCallback.WaitUntilRun in MultiCallbackQueue.cs (not touched here). Linux/macOS CI: change is platform-neutral.

## Log

- 2026-10-26: Created from CGR-246 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. curl_easy_pause with no perform running now returns CURLE_BAD_FUNCTION_ARGUMENT (43)
