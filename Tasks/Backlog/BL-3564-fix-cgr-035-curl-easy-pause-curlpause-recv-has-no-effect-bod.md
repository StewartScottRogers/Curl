---
id: BL-3564
title: Fix CGR-035: curl_easy_pause(CURLPAUSE_RECV) has no effect: body bytes and the DONE message are delivered while paused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitTests/Cgr035Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3564 — Fix CGR-035: curl_easy_pause(CURLPAUSE_RECV) has no effect: body bytes and the DONE message are delivered while paused

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-035 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-035: curl_easy_pause(CURLPAUSE_RECV) has no effect: body bytes and the DONE message are delivered while paused

- **Severity:** High
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-4`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- `C_31_pause_api_recv`: the server sends the head at once and a 30000-byte body after 300 ms. After the first perform, the application calls `curl_easy_pause(e, CURLPAUSE_RECV)` and runs perform + poll(100) 8 times. It counts the bytes the write callback received and checks info_read, then calls `curl_easy_pause(e, CURLPAUSE_CONT)` and finishes.
- `C_34_pause_call_inside_write`: the write callback calls `curl_easy_pause(e, CURLPAUSE_RECV)` on its first call and returns the full length. The main loop unpauses at iteration 5.

Command: `probe.exe C_31,C_34 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_31,C_34 2
```

**Expected (real curl)**

- C_31: `bytes-while-paused=0 msgs-while-paused=none`; after the unpause, `total=30000 intact=True msgs=0`.
- C_34: `calls-while-paused=1`, then the full 40000 bytes, intact.

**Actual (Curl)**

- C_31: `bytes-while-paused=30000 msgs-while-paused=yes`.
- C_34: `calls-while-paused=-1`, meaning the transfer completed before the unpause point.
- Pausing by returning CURL_WRITEFUNC_PAUSE or CURL_READFUNC_PAUSE does work.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEasyTransfer.Lifecycle.cs:56-60`. `Pause` only sets `pauseGate`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr035Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-035 of the third-party gap report.
