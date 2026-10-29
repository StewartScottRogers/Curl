---
id: BL-3677
title: Fix CGR-148: The socket callback is invoked from curl_multi_perform, and a -1 return fails the transfer with 42 (REF leaves it running or completes it)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr148Tests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Limits.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3677 — Fix CGR-148: The socket callback is invoked from curl_multi_perform, and a -1 return fails the transfer with 42 (REF leaves it running or completes it)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-148 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-148: The socket callback is invoked from curl_multi_perform, and a -1 return fails the transfer with 42 (REF leaves it running or completes it)

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-9`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- cases `C_23_sock_cb_abort` and `C_78_sock_cb_abort_long`. SOCKETFUNCTION returns -1 on its first call, or TIMERFUNCTION returns -1 on its second. The multi is driven for 3 s, either with `socket_action(CURL_SOCKET_TIMEOUT)` every 100 ms or with perform + poll. Command: `probe.exe C_23,C_78 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_23,C_78 2
```

**Expected (real curl)**

- socket_action: `codes 11/1 0/1 0/1 ...` (CURLM_ABORTED_BY_CALLBACK once, then 0 with running=1), no DONE message, and the callback is not called again.
- curl_multi_perform: `socket cb calls=0`, and the transfer completes with result 0.

**Actual (Curl)**

- socket_action: `codes 11/1 11/0 0/0 ... msgs 42`, and the callback gets a second call (`S=4`, CURL_POLL_REMOVE).
- curl_multi_perform: `socket cb calls=2 codes 11/1 0/0 msgs 42`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:684` (`UpdateSockets` runs on every perform), `:726-737` and `:749-767`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr148Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened (no task in Doing on origin/work/dark-factory names these files): the ManagedMultiTransferTests partial files whose tests pinned the old behaviour (perform telling the socket callback, a dead handle ending transfers with 42 on the next socket action) were moved to socket actions or replaced by Cgr148Tests; `ManagedMultiTransferTests.cs` and `.Timers.cs` only make `ControlledEasyTransfer` and `NewMulti` internal so Cgr148Tests can use them; the engine CLAUDE.md socket-action paragraph is kept true.
- Measured real libcurl 8.22.0 (Git for Windows ucrt64 `libcurl-4.dll`, Schannel) with a throwaway C# file-based P/Invoke probe (outside the repo) against a loopback server answering `200 hello`, each mode driven 2 s; the SCR probe was gone. REF:
  - socket action every 100 ms, SOCKETFUNCTION -1 on its first call: `codes 11/1 0/1 0/1 ...`, no message, socket cb 1 call, timer cb 1 call (the add's 0).
  - perform + poll with SOCKETFUNCTION set: `codes 0/1 0/1 0/0`, `CURLMSG_DONE` 0, socket cb 0 calls.
  - socket action, TIMERFUNCTION -1 on its second call: `codes 11/1 0/1 0/1 ...`, no message, no callback after the abort.
  - perform + poll, TIMERFUNCTION -1 on its second call: `codes 11/1 0/0 0/0`, `CURLMSG_DONE` 42, timer cb 2 calls in all.
- PORT before the fix (Cgr148Tests at HEAD, all 4 failed): perform called the socket callback; after a callback's -1 the next socket action answered 11 with 0 running, queued 42 and called `CURL_POLL_REMOVE`, as the report says. PORT after: the four Cgr148Tests pin exactly REF's codes, running counts, messages and callback sequences above (the engine's timer values differ only by its 10 ms check interval, ADR-0599). No bytes on the wire change: the fix touches only callback and message bookkeeping.
- Decision (ADR-0863, amends ADR-0599): perform never calls the socket callback; once a callback answers -1 the handle is dead - socket actions answer 0 with the transfers still running, no message, no callback; a later perform ends them with 42 and calls no callback.
- Variants that already matched still match: the perform + timer-abort case (11 then 42) kept its codes; every other ManagedMultiTransfer, Libcurl and Conformance test passes.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: first run flagged MoveTransfersOnAsync complexity 12 and a TellSocketCallback branch; split into MoveOnAndTellCallbacks/TellCallbacks/UnfinishedCount and added a test; second run 100% line, 100% branch, 0 failing, worst CRAP 10.
- Build clean; fast tests: 46 projects passed, 0 failed (Curl.Engine.UnitTests 2659 passed). The change is platform-neutral (no paths, sockets or OS text), which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-148 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. curl_multi_perform no longer calls the socket callback and a multi handle a callback killed answers socket actions with 0, no message and no callback, as libcurl 8.22.0
