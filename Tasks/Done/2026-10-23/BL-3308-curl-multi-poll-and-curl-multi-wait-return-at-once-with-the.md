---
id: BL-3308
title: curl_multi_poll and curl_multi_wait return at once with the busy transfer's socket ready, as libcurl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Multi/Wait, Libcurl.UnitTests/Multi, Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/MultiPollTimeoutWaitTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3308 — curl_multi_poll and curl_multi_wait return at once with the busy transfer's socket ready, as libcurl 8.22.0 does

## Goal

curl_multi_poll and curl_multi_wait, called with no extra sockets while a transfer is waiting on a slow body, return at once with numfds 1 as libcurl 8.22.0 does, instead of sleeping the whole timeout with 0.

## Context

Found by BL-3238's differential run (ADR-0793): m_in.json case 24 (/slow, add, perform, pollms 800 twice, waitms 800). Reference: `poll=0/1/0 poll=0/1/0 wait=0/1/0`; Curl's Libcurl: `poll=0/0/4 poll=0/0/4 wait=0/0/4` (ready count / elapsed in 200 ms buckets). Replay with LibcurlMultiDifferentialTests (CURL_DIFFERENTIAL_REFERENCE_LIBRARY). Start in Libcurl/Multi/Wait/CurlMulti.Wait.cs and the engine multi transfer's wait.

## Acceptance criteria

- [x] A unit test in Libcurl.UnitTests shows poll and wait return with numfds 1 before the timeout while the transfer's socket is waited on.
- [x] LibcurlMultiDifferentialTests no longer reports m_in case 24's poll and wait tokens.
- [x] Build clean; fast tests pass.

## Notes

- Cause: libcurl's first perform of a transfer only begins its non-blocking connect, so until the next perform the transfer's socket is writable and a wait or poll with no extra sockets returns at once with numfds 1. The managed engine runs transfers in the background and counted nothing.
- Fix (Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, not Libcurl/Multi/Wait: the wait lives in the engine): `MultiEntry.SocketReadyUntilNextPerform` is set when a perform starts the transfer and cleared by the next perform; `CountReady` adds each such unfinished transfer to a wait's ready count, so the wait returns at once. Later performs wait as before, so a perform-and-poll loop does not spin. touches widened to the engine file and the two engine tests that performed once then waited (they now perform twice first).
- Decision (default taken, no ADR needed for a bug fix to match libcurl): limited to the first perform, as measured by the recorded case m_in 24 (one perform, repeated polls all return 1 at 0 ms).
- Criterion 2: no reference libcurl (CURL_DIFFERENTIAL_REFERENCE_LIBRARY) exists on this machine, so the replay is Inconclusive here; Curl's side now gives the recorded reference tokens' shape (`0/1/0` for perform once then poll, poll, wait), pinned by CurlMultiWaitFirstPerformTests.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary reports 0 failing members; fast tests all green.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: aa393ce7 6937f422 b08b61f0.
- 2026-10-23: Doing -> Done. multi poll/wait return at once with numfds 1 after a transfer's first perform, as libcurl 8.22.0
