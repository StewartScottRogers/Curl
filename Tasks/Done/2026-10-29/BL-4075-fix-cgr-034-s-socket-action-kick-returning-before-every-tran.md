---
id: BL-4075
title: Fix CGR-034's socket-action kick returning before every transfer waits on its real socket (C_75, C_21)
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/Cgr034KickSettleTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4075 — Fix CGR-034's socket-action kick returning before every transfer waits on its real socket (C_75, C_21)

## Goal

With real sockets through `Libcurl`, the first `curl_multi_socket_action(CURL_SOCKET_TIMEOUT)` after the handles are added returns only once every transfer waits on its real socket, so its socket callbacks and the timer it tells match curl 8.22.0 every run.

## Context

Found by BL-3978's CGR-034 probe (source and results in `Documentation/Reviews/Cgr034ProbeResults.md`), against REF `libcurl-4.dll` 8.22.0 (Git for Windows ucrt64). REF, every run: `C_75` (`CURLOPT_TIMEOUT_MS` 600, server accepts and stays silent) gives `T0 add S1=2 T400+ kick S1=1 S1=4 T-1`, 28; `C_21` (three handles) tells all three `S=2` and then `T-1` inside the kick. PORT sometimes returns from the kick before a transfer has reached its socket wait, so it tells `T0` and announces the socket on a later action: `C_75` gave `T0 add T0 kick S1=2 T400+ S1=1 S1=4 T-1` once and `T0 add S1=2 T0 kick S1=1 T400+ S1=4 T-1` once; one `C_21` run of three gave `S1=2 S2=2 T0 kick ... S3=2 T-1 ...`. Results and bytes on the wire already match. The race is between the transfer's own task moving through connect/send and `SettleSocketWaitingTransfers`/`IsSettled` in `ManagedMultiTransfer.MovingOn.cs` deciding it has settled (for example a connect that completes at once on loopback leaving `Waiting` briefly `None` before the In wait, or settle seeing `Out` and stopping). The probe is a C# file-based app: copy it out of the review document to `.cgr034probe/probe.cs` and run `dotnet run probe.cs -- PORT x C_75,C_75,C_75,C_21,C_21,C_21`.

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests/Cgr034KickSettleTests.cs` pins that the kick tells each transfer's socket and the deadline (not `T0`) when its connect completes at once and it then waits to read.
- [x] The probe's PORT `C_75` and `C_21` give REF's sequences in three runs each (removal order in `C_21` follows completion order and varies in REF too).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate for the changed files.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3978's run (lane 2); BL-3978 depends on it.
- Cause: the kick settled a transfer on its connecting Out wait before the connect finished and the transfer moved on to its read wait. `Settle` now gives a connecting transfer a 50 ms grace (`ConnectGraceMilliseconds`) before that wait counts as settled. Probe PORT C_75 and C_21 matched REF in 9 of 9 runs after the change (the one `T0` seen was in the first, cold run, before a rerun of three). Quality run: no member outside a gate.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 7a3f6d23 531793ee b472e15b 779790c0.
- 2026-10-29: Doing -> Done. the first socket-action kick waits for each connecting transfer to reach its real socket wait, so C_75 and C_21 match curl 8.22.0
