---
id: BL-4593
title: Fix Mqtt5 conformance: Mqtt5:RFC6455-4.1-9 and Mqtt5:RFC6455-4.1-10 (upgrade reply without Upgrade or Connection)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketUpgrade.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketMessages.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4593 — Fix Mqtt5 conformance: Mqtt5:RFC6455-4.1-9 and Mqtt5:RFC6455-4.1-10 (upgrade reply without Upgrade or Connection)

## Goal

curl's mqtt5+ws:// fails the WebSocket connection when the 101 reply lacks `Upgrade: websocket` or `Connection: Upgrade` (RFC 6455 section 4.1, items 2 and 3 of the client's response checks).

## Context

Found by BL-4486: `Mqtt5WebSocketUpgrade.CheckHead` checks only the status, Sec-WebSocket-Accept and Sec-WebSocket-Protocol, so `WebSocketCases.Handshake_ReplyWithoutUpgradeWebsocket_FailsBeforeAnyFrame` and `Handshake_ReplyWithoutConnectionUpgrade_FailsBeforeAnyFrame` (Curl.Conformance.Extended.UnitTests/Mqtt5/Transports) see exit 0. Both values compare case-insensitively (RFC 6455 4.1); exit 8 with a message in `Mqtt5WebSocketMessages` like the Accept mismatch's.

## Acceptance criteria

- [ ] Both cases pass and the Mqtt5:RFC6455-4.1-9 and Mqtt5:RFC6455-4.1-10 lines are removed from Curl.Conformance.Extended.UnitTests/KnownFailures.txt.
- [ ] A focused unit test in Curl.Extended.Protocol.Mqtt5.UnitTests pins each check (ADR-0482).
- [ ] dotnet build is clean, the fast tests are green and Curl.Extended.Protocol.Mqtt5.UnitLibrary stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f44d6cb0 be93e6c9 91dca242.
