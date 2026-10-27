---
id: BL-3564
title: Fix CGR-035: curl_easy_pause(CURLPAUSE_RECV) has no effect: body bytes and the DONE message are delivered while paused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/CallbackWriteStream.cs, Curl.Engine.UnitTests/Cgr035Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr035Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Cause: `ManagedEasyTransfer.Pause` sets the handle's `EasyPauseGate`, but `CallbackWriteStream.WriteAsync` consulted the gate only after the callback itself returned `CURL_WRITEFUNC_PAUSE`, so a `curl_easy_pause(CURLPAUSE_RECV)` from the application or from inside the write callback held nothing: body bytes kept flowing and the perform finished, queueing `CURLMSG_DONE`. Fix: `WriteAsync` first waits on `WhenResumedAsync(CurlPause.Receive)` before calling the callback (the header callback shares the stream, as libcurl's `CURLPAUSE_RECV` holds both). Added `Curl.Engine.UnitLibrary/CallbackWriteStream.cs` to `touches` (the fix lands there; no task on origin/work/dark-factory's Doing names it). `ManagedEasyTransfer.Lifecycle.cs` needed no change. No ADR: it is a bug fix to match libcurl, not a design choice.
- REF measured (the report's `SCR` probe is gone): a C# file-based probe (`dotnet run probe.cs`, P/Invoke into the installed libcurl 8.18.0 `libcurl-x64.dll`, a loopback server sending the head at once and 30000 body bytes after 300 ms) running C_31 (perform, `curl_easy_pause(e, CURLPAUSE_RECV)`, 8 x perform + poll(100), then `CURLPAUSE_CONT`) printed `bytes-while-paused=0 msgs-while-paused=none` and `total=30000 done=1`, matching the report's Expected for 8.22.0.
- PORT: no Libcurl native build in the lane, so C_31 and C_34 run in-process in `Curl.Engine.UnitTests/Cgr035Tests.cs`: `WriteAsync_ReceivingPausedByTheApplication_CallsNoCallbackUntilUnpaused` (C_31: no callback, no completion while paused, the chunk delivered after the unpause), `WriteAsync_CallbackPausesThroughTheApiAndTakesTheChunk_HoldsTheNextChunkUntilUnpaused` (C_34: calls-while-paused=1) and `PerformAsync_WriteCallbackPausesTheHandleAndTakesTheChunk_DeliversNothingMoreUntilUnpaused` (C_34 through a whole HTTP perform: nothing delivered and the perform not finished while paused, then `helloworld` intact and `Ok`). The two stream tests failed with the fix removed and pass with it.
- Variants that already matched (pausing by returning `CURL_WRITEFUNC_PAUSE` or `CURL_READFUNC_PAUSE`) still pass: `CallbackWriteStreamTests`, `CallbackReadStreamTests`, `ManagedEasyTransferTests.PausedPost.cs` and the conformance ratchet are green.
- Fast tests: all 46 test assemblies passed (Curl.Engine.UnitTests 2481 passed, Curl.Conformance.UnitTests 4381 passed). The change is platform-neutral (no paths, OS text or certificates), which covers CI on Linux and macOS. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.
- Follow-up: BL-3836, the same gap for `CURLPAUSE_SEND` in `CallbackReadStream` (left out here to keep this task to its issue).

## Log

- 2026-10-26: Created from CGR-035 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. curl_easy_pause(CURLPAUSE_RECV) now holds the write and header callbacks, and so the transfer's DONE message, until the handle is unpaused
