---
id: BL-1647
title: Stop SwsHttpServerConnector losing recorded bytes when connections write on many threads at once
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1647 — Stop SwsHttpServerConnector losing recorded bytes when connections write on many threads at once

## Goal

`SwsHttpServerConnector.ReceivedBytes` holds every byte the client wrote, in write order, when its connections are written on many threads at once, as they are when curl runs transfers in parallel.

## Context

- Found by BL-1494 (adversarial black-box tests of `Curl.Conformance.UnitLibrary`).
- `SwsServerRecording` (`Curl.Conformance.UnitLibrary/SwsServerRecording.cs`) keeps the bytes in a plain `List<byte>` and `Record` calls `AddRange` with no lock; every `SwsHttpServerConnection` the connector opens shares it, and `RecordDisconnect` and `Bytes` touch it the same way. Concurrent `AddRange` calls on a `List<T>` lose items and can throw.
- Reproduction (public surface only): 300 rounds of a new `SwsHttpServerConnector` with one `<data>` part; 32 `Task.Run` tasks each `ConnectAsync` and write `GET /1234 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n` one byte per `WriteAsync`; assert `ReceivedBytes.Length == 32 * request length`. It failed on the first run (2026-10-07) with fewer bytes recorded than written.
- BL-1494 left a single-threaded interleaving test (`ManyConnectionsWithInterleavedHalfRequests_EachIsAnsweredAndEveryByteIsRecorded`) whose comment names this task; the threaded test lands with the fix.

## Acceptance criteria

- [x] A test in `Curl.Conformance.UnitTests` writes on 32 connections from 32 threads for at least 100 rounds and asserts every byte is recorded; it fails before the fix and passes after.
- [x] Recording, the disconnect marker and `ReceivedBytes` are safe to call from many threads at once (a lock in `SwsServerRecording`, or equivalent), and the bytes of one `WriteAsync` stay contiguous.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Fix: `SwsServerRecording` takes one `System.Threading.Lock` in `Record`, `ArmDisconnectMonitor`, `RecordDisconnect` and `Bytes`; one `Record` call is one `WriteAsync`, so its bytes stay contiguous. The monitor flag sits under the same lock so arming and the disconnect line cannot interleave.
- Test: `SwsHttpServerConnectorTests.ThirtyTwoConnectionsWrittenOneBytePerWriteOnThirtyTwoThreads_RecordEveryByte` (100 rounds, 32 `Task.Run` writers, one byte per write); lane 9 saw it fail before the fix and pass after.
- Lanes 9 and 8 finished this but could not integrate because `Curl.Cookies.UnitTests` (`EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother`) failed, a flaky test outside this task that parked other tasks too. Lane 1 re-applied the work from factory/BL-1647-lane-8-20261007-111121; build clean, all 33 fast test projects green, Cookies included.
- Measured (lane 8, same code): `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 100% line, 100% branch, 0 failing members. Not re-measured: the code is identical.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 9 could not integrate: fast tests failed twice (Curl.Cookies.UnitTests failed; then no test named) after rebasing onto the other lanes' work. The work is on branch factory/BL-1647-lane-9-20261007-111121; start with git cherry-pick --no-commit factory/BL-1647-lane-9-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 8 could not integrate: fast tests failed twice (EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then Curl.Cookies.UnitTests failed) after rebasing onto the other lanes' work. The work is on branch factory/BL-1647-lane-8-20261007-111121; start with git cherry-pick --no-commit factory/BL-1647-lane-8-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. SwsServerRecording locks every member, so parallel writes on 32 threads keep every recorded byte
