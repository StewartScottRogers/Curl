---
id: BL-4449
title: Re-fix AF-0420: pin that SwsHttpServerConnection closes the connection after a closing reply and keeps it open otherwise
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs, Curl.Conformance.UnitTests/SwsHttpServerConnectionTests.cs]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4449 — Re-fix AF-0420: pin that SwsHttpServerConnection closes the connection after a closing reply and keeps it open otherwise

## Goal

A test fails if `SwsHttpServerConnection.Send` stops ending the connection after a closing reply, or ends it after a reply that does not close.

## Context

Audit finding AF-0420 (accepted 2026-10-24, quality auditor, Medium): `Send`'s closing `SwsServerSend(..., true)` could become `false` with no test failing, so a closing reply (`swsclose`, close after the reply) never marked the close. BL-3469 rewrote `Send` (`Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs`, about line 424): it now takes `bool closes` and, under `if (closes)`, sets `readsRequests = false`, queues an empty `SwsServerSend([], serverBusyUntil)` and calls `RecordDisconnectOnce()`. The `true` literal is gone, so 8 re-audits in a row reported "site missing" and the finding stays open. Stewart chose a re-fix on 2026-11-01. On 2026-11-01 the mutant `readsRequests = false` -> `true` (line 444) timed out rather than failing a test, and no mutation operator reaches the empty close send at line 445, so nothing asserts the close itself.

## Acceptance criteria

- [x] A test (in a new `Curl.Conformance.UnitTests/SwsHttpServerConnectionTests.cs`, or the existing connector tests if the connection is only reachable through them) sends a request whose reply closes the connection and asserts, within a bounded time and without hanging, that the server side ends the connection after the reply bytes and records the disconnect once.
- [x] A second test sends a request whose reply does not close and asserts the connection stays open and a second request on it is answered.
- [x] Deleting the `pendingSends.Add(new SwsServerSend([], serverBusyUntil));` line under `if (closes)` (a temporary local edit, reverted) makes the first test fail rather than hang; Notes record the failure message.
- [x] `dotnet build` is clean, the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, and the quality gates hold for `Curl.Conformance.UnitLibrary`.

## Notes

- Tests went into the new `Curl.Conformance.UnitTests/SwsHttpServerConnectionTests.cs` and go through `SwsHttpServerConnector`, the only way to build a connection with its reply selector, recording and abandonment. Every read after the reply is bounded by `WaitAsync(10 s)`, so a broken close fails with a `TimeoutException` instead of hanging.
- The closing test uses `<postcmd>` `wait 1` on a `ManualTimeProvider`. The close is the empty send queued at `serverBusyUntil`, so it is the only thing that holds the 0 back until the wait is over. Without it, a read finds nothing owed and returns 0 straight away.
- Mutant check (temporary local edits, reverted; the file has no diff): deleting `pendingSends.Add(new SwsServerSend([], serverBusyUntil));` fails `Send_ClosingReply_EndsTheConnectionAfterTheReplyAndRecordsTheDisconnectOnce` in 52 ms with "Assertion failed. Expected condition to be false. Assert.IsFalse(closedBeforeItsTime)". The `readsRequests = false` -> `true` mutant now fails the same test with "Expected strings to be equal. Assert.AreEqual(string.Empty, afterTheClose)" instead of timing out.
- No production code changed, so the quality gates for `Curl.Conformance.UnitLibrary` stay as they were and no Measure-CodeQuality run was needed. `dotnet build` is clean (0 warnings) and the fast tests are green on Windows. The tests use no path, file or OS text, so they are platform-neutral.

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: d21bcb31 faa39c5a 877223ce 861d05b4.
- 2026-11-01: Doing -> Done. SwsHttpServerConnectionTests pins the closing reply's end of connection and single disconnect, and the open connection serving a second request; both AF-0420 mutants fail it
