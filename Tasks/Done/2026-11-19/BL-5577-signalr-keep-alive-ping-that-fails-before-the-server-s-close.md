---
id: BL-5577
title: SignalR keep-alive Ping that fails before the server's Close is decoded must not fail a finished transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5577 — SignalR keep-alive Ping that fails before the server's Close is decoded must not fail a finished transfer

## Goal

A keep-alive Ping write that fails while the server's Close frame is still in flight (sent but not yet decoded by the read loop) no longer fails a SignalR transfer the hub finished normally.

## Context

Found by the shift-end review of 2026-11-19 (silent-failure-hunter) on the BL-5575 change (commit 9e3ea4532a).

`Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs` `TrySendPingAsync` (around line 127) swallows a Ping `IOException` only `when (session.CloseReceived)`. `SignalRWebSocketSession.CloseReceived` is set only once the read loop has decoded the whole Close frame (`Transport/SignalRWebSocketSession.cs` line 126).

Failure: the server sends its Completion and Close, then resets the connection. The Ping write fails while those bytes are still unread, so `CloseReceived` is false and the Ping task faults. The read loop then reads the Close and ends normally, and `await pinging` in `RunAsync` (line 63) rethrows. The transfer fails after the hub finished it - the same failure BL-5575 fixed, with a smaller window.

Fix direction: decide the Ping failure's fate after the read loop ends. When `reading` completed through the server's Close (`session.CloseReceived` is true then), a Ping `IOException` is the hub going away, not an error.

## Acceptance criteria

- [x] A new test in `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs` fails a held Ping write with an `IOException` *before* giving the server's Close frame, then gives the Close, and asserts exit code `Ok`; it fails on the code before the fix.
- [x] A Ping write that fails while the hub stays open, with the read loop then ending without a server Close, still fails the transfer (the existing `RecvError` keep-alive test still passes).
- [x] The comments in `RunAsync` and `TrySendPingAsync` say when a Ping failure is ignored.
- [x] `dotnet build` is clean and the fast tests pass; `Curl.Extended.Protocol.SignalR.UnitLibrary` keeps 100% line and branch coverage.

## Notes

Ping failure fate is now decided in RunAsync after the read loop ends (AwaitPingingAsync); TrySendPingAsync no longer catches. Quality run: 0 failing members.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 3d5183df 5a0fff3c 4e85d062 cd3374e2.
- 2026-11-19: Doing -> Done. Ping failure decided after read loop
