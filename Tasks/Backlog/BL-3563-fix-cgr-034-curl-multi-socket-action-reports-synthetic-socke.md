---
id: BL-3563
title: Fix CGR-034: curl_multi_socket_action reports synthetic socket numbers, never CURL_POLL_OUT, and a 10 ms polling timer instead of libcurl's timeouts
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr034Tests.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3563 — Fix CGR-034: curl_multi_socket_action reports synthetic socket numbers, never CURL_POLL_OUT, and a 10 ms polling timer instead of libcurl's timeouts

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-034 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-034: curl_multi_socket_action reports synthetic socket numbers, never CURL_POLL_OUT, and a 10 ms polling timer instead of libcurl's timeouts

- **Severity:** High (applications that run their own event loop, such as libuv, libevent or epoll, cannot drive transfers)
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-3`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- cases `C_20_sockaction_one`, `C_21_sockaction_three_assign`, `C_65_sockaction_refused`, `C_74_timer_only_perform` and `C_75_sockaction_timeout_transfer`.
  1. The application sets SOCKETFUNCTION and TIMERFUNCTION and adds 1 or 3 easy handles.
  2. It runs a standard event loop: WSAPoll or poll() on the sockets it was given; `curl_multi_socket_action(fd, ev)` on each event; `curl_multi_socket_action(CURL_SOCKET_TIMEOUT)` when the timer expires.
  3. It checks every socket with `getsockopt(SO_TYPE)`.

  Command: `probe.exe C_20,C_21,C_65,C_74,C_75 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_20,C_21,C_65,C_74,C_75 2
```

**Expected (real curl)**

- Sockets are real and poll() drives the transfer: `real: S1:real`, `pollErrors>0=False socketEvents>0=True`.
- Callback sequence: `add T0 kick S1=2 T-1 S1=1 S1=4`.
- TIMEOUT_MS=600 against a silent server: `T0 S=2 T400+ S=1 S=4 T-1 results=28`.
- Refused connect: `T0 S=2 T-1 S=4 results 7`.
- TIMERFUNCTION only, driven by perform: `T0 T-1`.

**Actual (Curl)**

- Sockets are engine numbers from 0x40000000 upward, so poll() fails on them and no socket event ever fires: `real: S1:NOT-A-SOCKET`, `pollErrors>0=True socketEvents>0=False`.
- Callback sequence: `add T0 kick S1=1 T+ S1=4 T-1`. The socket is registered for CURL_POLL_IN at once and never for CURL_POLL_OUT.
- While anything is running, the timer is re-armed every 10 ms (`T+*`, `T0*`) instead of getting -1 or the transfer's real timeout.
- Transfers finish only because the application busy-polls through the timer.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs`:
  - `:71` `FirstTransferSocket`;
  - `:769-786` `TellSocketCallback` always passes `CurlPoll.In`;
  - `:812-822` `TimerMilliseconds` uses `RunningTransferCheckInterval` (10 ms, defined at `:92`).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr034Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5, first pass, no code changed): the fix cannot live in `ManagedMultiTransfer.cs`. Its synthetic sockets and 10 ms timer are ADR-0599/ADR-0610 design: `Curl.Networking` never hands out its `Socket`, and each transfer runs as its own async task that consumes data whenever it comes, so even with real sockets a level-triggered poll would never wake the application. Filed BL-3846 (expose the OS socket handle through `IConnection`, across Abstractions, Networking and the engine wrappers) and BL-3847 (gate socket-action transfers' I/O on readiness signals and publish OUT/IN and the earliest deadline). Once both are Done this task wires `ManagedMultiTransfer` to them: real socket in the socket callback, `CurlPoll.Out` then `In`, the timer from the deadline (-1 when none), and drops `RunningTransferCheckInterval`. The probe needs rebuilding as a C# file-based app (P/Invoke to a real libcurl-4.dll against `Libcurl`) with the expected sequences in the issue.
- 2026-10-26 (lane 5, second claim): wired `ManagedMultiTransfer` to `TransferSocketWait` (ADR-0828, in the stash): a `ManagedEasyTransfer` started while `MultiOptions.Socket` is set gets a wait (`SocketWaitFor`); `SocketActionGatedTcpDialer` passes the connection's `SocketHandle` to `EndConnect`, kept as `TransferSocketWait.Socket`; `SocketActionAsync` signals the wait whose socket it names (no events: the directions it waits in), `PerformAsync` signals every wait; each call then lets such transfers run to their next socket wait or end, running their queued callbacks, for at most `SocketWaitSettleLimit` (200 ms), and queues the done messages of transfers started before the call; the socket callback gets the real socket with `Waiting` (Out, then In) when it changes; the timer is the earliest `EarliestDeadline` (-1 when none) when every running transfer waits on its socket, the handle's timeout with no socket callback, else the 10 ms fallback, and a positive deadline is not re-told while its due time moves by at most 1 ms. Added `TransferSocketWait.cs` and `SocketActionGatedTcpDialer.cs` to touches (no task in Doing on origin/work/dark-factory names them). `Cgr034Tests` (4 tests) pins `T0 S777=2 T-1 S777=1 S777=4` for one transfer, `T0 S=2 T400+ S=1 ... S=4 T-1` and 28 for the silent-server timeout, `T0 T-1` for timer-only perform, and perform alone finishing a gated transfer. Curl.Engine.UnitTests 2565 passed, Libcurl.UnitTests 1428 passed with it.
- Requeued for BL-3890: the refused-connect case `C_65` (curl `T0 S=2 T-1 S=4`) needs the socket while it connects, which `Curl.Networking`'s `TcpDialer` does not publish; this run gives `T0 T0 T-1` and 7 there. Still to do once BL-3890 is Done: apply the stash, report the connecting socket (it should need no `ManagedMultiTransfer` change), run Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary (not yet run), build the probe as a C# file-based app (P/Invoke to a real `libcurl-4.dll` for REF and `Libcurl` for PORT, the recording server on 127.0.0.1:21700/21701, 21799 closed) and compare `C_20`, `C_21`, `C_65`, `C_74`, `C_75` for criterion 1; `C_21` (three handles, `curl_multi_assign`) is not yet covered by a unit test.

## Log

- 2026-10-26: Created from CGR-034 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Needs BL-3846 (real OS socket through IConnection) and BL-3847 (readiness-gated socket-action I/O with OUT/IN and deadline) first
- 2026-10-26: BL-3847 added BL-3859 to depends-on: the gate's routing into the dialer is BL-3859's.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Needs BL-3890 (the socket while it connects, for C_65) first; the multi wiring is in the stash
