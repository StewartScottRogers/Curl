---
id: BL-1067
title: Find and fix the Curl.Quic.UnitTests fast run that hung on a second dotnet test
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1067 — Find and fix the Curl.Quic.UnitTests fast run that hung on a second dotnet test

## Goal

`Curl.Quic.UnitTests` finishes every fast run: no test can wait forever, so `dotnet test --filter "TestCategory!=Integration"` never hangs in it.

## Context

- Seen 2026-09-30 on dark factory lane 1 (BL-886, a comment-only change): the first fast run of `Curl.slnx --no-build` passed `Curl.Quic.UnitTests` (405 of 405, 804 ms); an immediate second run of the same build hung in `Curl.Quic.UnitTests`'s `testhost.exe` for the whole 3600 s tool timeout and was killed. Every other project had finished.
- Suspects: a test awaiting a `TaskCompletionSource`, channel read or fake `TimeProvider` timer that a race leaves unsignalled, or a loopback UDP socket that never receives. Start by running the project in a loop (`for ($i=0;$i -lt 50;$i++){ dotnet test Curl.Quic.UnitTests --no-build --filter "TestCategory!=Integration" --blame-hang-timeout 2m }`) and read the blame dump's hung test.

## Acceptance criteria

- [x] The hanging test is named in Notes, with the cause.
- [x] 50 consecutive `dotnet test Curl.Quic.UnitTests --filter "TestCategory!=Integration" --blame-hang-timeout 2m` runs all pass with no hang.
- [x] Every awaited wait in `Curl.Quic.UnitTests` that can hang is bounded (a `[Timeout]` or a cancellation token), so a regression fails instead of hanging.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- **Hanging tests.** `QuicConnectionTests.ReadAsync_ServerBreaksFlowControl_FailsTheConnectionWithRecvError` (4 of 5 hangs caught) and `QuicConnectionTests.AcceptUnidirectionalStreamAsync_ServerClosesTheConnection_FailsWithRecvErrorAndSendsNoClose` (1 of 5). Isolated runs never hung (50 of 50 passed, and 48 of 48 with 8 runs at once). It hung about 1 run in 35 to 140 under `DOTNET_PROCESSOR_COUNT=2`, the starved thread pool a whole-solution run gives it.
- **Cause.** The datagram the test's server sends (`QuicTestLiveChannel.FromServer`) was lost before the client read it. `QuicConnection`'s loop cancels its pending receive on every wake, and the test wakes it (`ReadAsync`/`AcceptUnidirectionalStreamAsync` call `Wake()`) just before `FromServer` writes. `QuicTestLiveChannel` queued datagrams in an unbounded `System.Threading.Channels` channel, and on .NET 10.0.12 a channel can lose an item written just after a waiting `ReadAsync` is cancelled. A log of the channel showed it on every stall: read waiting, `TryWrite` of the 143-byte packet returned true, that read threw `OperationCanceledException`, and the next read waited on an empty queue. The client had recorded packet 0 but never packet 1, though packet 1 unprotected fine afterwards. The connection never failed, so the awaited read never completed. Nothing referenced the task chain any more, so the GC collected it, and a hang dump shows no test frame at all. A standalone repro (linked token cancelled, then `TryWrite`, 300,000 times, 2 processors with CPU noise) lost 11 items. The production `QuicConnection` is not at fault: a lost UDP datagram is normal there, and the server's retransmission recovers it. The test server never retransmits, because its clock never moves.
- **Fix.** `QuicTestLiveChannel` now queues in a `ConcurrentQueue` counted by a `SemaphoreSlim`. A cancelled `WaitAsync` takes no count, so the datagram stays queued for the next receive. In-process stress (3,000 flow-control failures, 8 at a time, 2 processors) went from a stall in 3 of 4 runs to none in 6 runs. `dotnet test Curl.Quic.UnitTests` passed 50 of 50 runs as written in the criterion, and 180 of 180 with 3 at once under `DOTNET_PROCESSOR_COUNT=2` (5 hangs in about 167 such runs before).
- **Bounds.** Each of the 20 async tests in `QuicConnectionTests` and `QuicClientConnectorTests` has `[Timeout(QuicTest.HangTimeoutMilliseconds)]` (2 minutes, longer than `QuicTestLiveChannel.HangGuard`, so the guarded waits fail first with their own message). The two `WaitingToReceive.WaitAsync()` waits in `RunHandshakeAsync_SilentPeer_ProbesTheInitialWithExponentialBackoff` now wait at most `HangGuard` and assert. Every other test in the project is synchronous. The `[Timeout]` is not cooperative, so MSTest abandons a hung test and fails it, which is the point.
- `QuicClientConnectorTests.cs` had LF line endings, which `dotnet format --verify-no-changes` rejects; it is CRLF now.
- Follow-up: four other test fakes read an unbounded `Channel<T>` with a cancellable token: in Console, Networking, Http and Ssh. They are outside this task's `touches`, so they are filed as BL-1068.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Curl.Quic.UnitTests no longer hangs: its live test channel keeps a datagram a cancelled receive would have lost, and every async QUIC test has a 2-minute [Timeout]
