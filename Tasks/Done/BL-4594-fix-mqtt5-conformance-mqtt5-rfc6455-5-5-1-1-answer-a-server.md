---
id: BL-4594
title: Fix Mqtt5 conformance: Mqtt5:RFC6455-5.5.1-1 (answer a server Close frame with a Close frame)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketConnection.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4594 — Fix Mqtt5 conformance: Mqtt5:RFC6455-5.5.1-1 (answer a server Close frame with a Close frame)

## Goal

curl's mqtt5+ws:// answers a server's Close frame with a masked Close frame before closing the connection (RFC 6455 section 5.5.1).

## Context

Found by BL-4486: `Mqtt5WebSocketConnection.Collect` sets `isClosed` on a Close frame but never sends one back, so `WebSocketCases.Frames_CloseFromTheServer_IsAnsweredWithACloseFrame` (Curl.Conformance.Extended.UnitTests/Mqtt5/Transports) fails. Echo the status code as RFC 6455 5.5.1 allows; keep the exit 56 "Connection disconnected" that `Frames_CloseFromTheServer_EndsTheSubscriptionWithExit56` pins.

## Acceptance criteria

- [x] The case passes and the Mqtt5:RFC6455-5.5.1-1 line is removed from Curl.Conformance.Extended.UnitTests/KnownFailures.txt.
- [x] A focused unit test in Curl.Extended.Protocol.Mqtt5.UnitTests pins the Close reply (ADR-0482).
- [x] dotnet build is clean, the fast tests are green and Curl.Extended.Protocol.Mqtt5.UnitLibrary stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

- Done directly (a one-file fix, so the feature pipeline's planning stages were skipped). `Collect` keeps the Close frame's first two payload bytes (the status code, RFC 6455 5.5.1 allows echoing it), or nothing when the frame has none; `AcceptAsync` then sends one masked Close frame back and flushes before the read ends. Exit 56 is unchanged. Unit tests: ReadAsync_CloseFrameWithAStatusCode_AnswersWithAMaskedCloseFrameEchoingIt and ReadAsync_CloseFrameWithoutAStatusCode_AnswersWithAnEmptyMaskedCloseFrame. Measure-CodeQuality for Curl.Extended.Protocol.Mqtt5.UnitLibrary: 0 failing members. Fast tests green on Windows; the change is platform-neutral (bytes only).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 1ac6766c 6c3b4c2b 91dca242.
- 2026-11-02: Doing -> Done. mqtt5+ws answers a server Close frame with a masked Close frame echoing its status code
