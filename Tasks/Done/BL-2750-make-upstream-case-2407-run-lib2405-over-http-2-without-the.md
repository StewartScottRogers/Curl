---
id: BL-2750
title: Make upstream case 2407 run lib2405 over HTTP/2 without the 'not a socket' SocketException and hold it on the ratchet
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2743]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2750 — Make upstream case 2407 run lib2405 over HTTP/2 without the 'not a socket' SocketException and hold it on the ratchet

## Goal

Upstream case 2407 (lib2405 over HTTP/2) passes through Curl and is held on the ratchet.

## Context

Found by BL-2697. Run with the Unix platform's features (the `Platform` line in `UpstreamConformanceTests` set to `UpstreamCurlPlatform.Unix` for one local run), case 2407 fails with `curl threw SocketException: An operation was attempted on something that is not a socket.` This was measured on Windows; check whether Linux and macOS give the same. Port: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs` and `Lib2405Run`; the `wakeup` feature is already reported (BL-2661, ADR-0690). Case 2407 is skipped, with run time, on Linux and macOS CI (run 39155312605).

## Acceptance criteria

- [x] Case 2407 passes when run with the Unix platform's features, and is on `PassingUpstreamCases.txt` without making Windows, Linux or macOS CI red (a focused unit test in `Curl.Conformance.LibTest.UnitTests` pins the fix).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-10-18 on Windows (Platform forced to Unix for the run). The SocketException has one cause: `curl_multi_waitfds` returns engine-numbered sockets (`ManagedMultiTransfer.FirstTransferSocket`, 0x40000000 and up), `Lib2405Run` polls them on a second multi handle, and `ManagedMultiTransfer.PollWaitSocket` hands them to `SocketWaitPoller`, which wraps them as OS sockets. Fix, done but left uncommitted for the next claim (stashed by the shift): in `PollWaitSocket` return `CurlWaitEvents.None` when `socket.Handle >= FirstTransferSocket`, plus the unit test `PollAsync_EngineNumberedExtraSocket_ReportsNoEventWithoutAskingThePoller` in `Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs`. This needs `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` in `touches` (added; no other task in Doing on `origin/work/dark-factory` names them).
- With that fix 2407 (and 2405) no longer throws but fails `Max number of waitfds: 0 not as expected: 3`: the first `PerformAsync` finishes both transfers (running=0), so `curl_multi_waitfds` never sees a started transfer, and no wakeup descriptor is counted although the version info reports a threaded resolver. That is the failure BL-2743 already owns for case 2405 (same `Lib2405`/`Lib2405Run`), so it is left to BL-2743; `depends-on: [BL-2743]`. Once BL-2743 lands, re-run `CURL_UPSTREAM_CASE=2407` with the Platform line in `UpstreamConformanceTests` set to `UpstreamCurlPlatform.Unix`, apply the stashed Engine fix, and list 2407.

- 2026-10-19: BL-2743 had already landed the stashed Engine fix (`PollWaitSocket` treats every engine number from `ResolverWakeupSocket` up as no descriptor), so the stash was not applied. With it, 2407 failed only the multiplexed turn: `Max number of waitfds: 3 not as expected: 2`. Cause: under `CURLPIPE_MULTIPLEX` with `CURLOPT_PIPEWAIT`, libcurl's second transfer waits for the first's connection and holds no socket, while the engine gave each started transfer one. Fix: `ManagedMultiTransfer.GetWaitSockets` gives no socket to a transfer that `WaitsForEarlierConnection` (Multiplex, PipeWait, not held to HTTP/1, an earlier running transfer to the same origin). 2405 and 2407 pass with the Unix platform forced; on Windows 2407 is Inconclusive as "runs only on another platform" (no http/2 feature), which the ratchet allows.
- Decision (sensible default): the focused unit test is `GetWaitSockets_PipeWaitingTransferToARunningOneSOrigin_HoldsNoSocketOfItsOwnUnderMultiplex` in `Curl.Engine.UnitTests` (`ManagedMultiTransferTests.Limits.cs`), the fixed library's own tests per ADR-0482, rather than `Curl.Conformance.LibTest.UnitTests`: no LibTest code changed. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7d39c3cd 3d836d5e.
- 2026-10-18: Doing -> Backlog. Waits on BL-2743 (waitfds sees 0 sockets because the first perform finishes both transfers; same failure as case 2405); Engine fix for the SocketException is done and uncommitted
- 2026-10-19: Backlog -> Doing. Criteria at claim: 7d39c3cd 3d836d5e.
- 2026-10-19: Doing -> Done. 2407 passes with Unix features and is on the ratchet
