---
id: BL-5580
title: SignalR keep-alive test waits for the Ping write to fail before giving the server's Close, not Task.Delay(50)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5580 — SignalR keep-alive test waits for the Ping write to fail before giving the server's Close, not Task.Delay(50)

## Goal

`PingWriteFailsBeforeTheServersCloseIsDecoded_DoesNotChangeTheExitCode_HubProtocolPing` always tests the scenario its name describes: the Ping write has failed before the server's Close frame is given, on any machine load, with no real-time delay.

## Context

The shift-end review of 2026-11-19 (pr-review-toolkit code-reviewer, confirmed by hand) flagged `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs:152`. BL-5577 added this regression test for `SignalRHubSession.AwaitPingingAsync`, which now forgives a Ping `IOException` when the read loop ended through the server's Close. The test calls `hub.ReleaseHeldWriteWithFailure()`, then `await Task.Delay(50)`, then `hub.Give(<Close frame>)`. Nothing confirms that the Ping task faulted before the Close was given. On a loaded CI machine the Close can be read first. The test still passes in that case, but only through the older path where the failure lands after `CloseReceived` is set, so it no longer guards BL-5577's fix. The same shift (BL-5578) removed exactly this `Task.Delay(50)` pattern from the neighbouring tests, and `.claude/rules/testing.md` asks for fakes over real time.

The held write in `BlockingConnection` fails through `release.TrySetException`, which does not count toward `FailedWrites`. So the fake needs an observable signal: for example a counter incremented when a held write completes with a failure, or a check on the hub session's Ping task. Without one, the test cannot wait on state.

## Acceptance criteria

- [ ] The test has no `Task.Delay` and waits through `WaitUntil` on observable fake state showing that the held Ping write has thrown before `hub.Give` sends the Close frame.
- [ ] Reverting `SignalRHubSession.AwaitPingingAsync` to a plain `await pinging` makes the test fail every run (check locally, then restore).
- [ ] `dotnet test Curl.Extended.Protocol.SignalR.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-19: Created.
