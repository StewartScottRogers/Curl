---
id: BL-4584
title: Fix Mqtt5 conformance: Mqtt5:RFC6455-5.5.1-1 (answer a server Close frame with a Close frame)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketConnection.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4584 — Fix Mqtt5 conformance: Mqtt5:RFC6455-5.5.1-1 (answer a server Close frame with a Close frame)

## Goal

curl's mqtt5+ws:// answers a server's Close frame with a masked Close frame before closing the connection (RFC 6455 section 5.5.1).

## Context

Found by BL-4486: `Mqtt5WebSocketConnection.Collect` sets `isClosed` on a Close frame but never sends one back, so `WebSocketCases.Frames_CloseFromTheServer_IsAnsweredWithACloseFrame` (Curl.Conformance.Extended.UnitTests/Mqtt5/Transports) fails. Echo the status code as RFC 6455 5.5.1 allows; keep the exit 56 "Connection disconnected" that `Frames_CloseFromTheServer_EndsTheSubscriptionWithExit56` pins.

## Acceptance criteria

- [ ] The case passes and the Mqtt5:RFC6455-5.5.1-1 line is removed from Curl.Conformance.Extended.UnitTests/KnownFailures.txt.
- [ ] A focused unit test in Curl.Extended.Protocol.Mqtt5.UnitTests pins the Close reply (ADR-0482).
- [ ] dotnet build is clean, the fast tests are green and Curl.Extended.Protocol.Mqtt5.UnitLibrary stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-11-02: Created.
