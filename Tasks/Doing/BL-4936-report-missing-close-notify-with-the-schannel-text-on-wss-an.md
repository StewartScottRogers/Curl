---
id: BL-4936
title: Report missing close_notify with the schannel text on wss and mqtts
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ws.UnitLibrary/WsIoFailures.cs, Curl.Original.Protocol.Mqtt.UnitLibrary/MqttTransferMessages.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4936 — Report missing close_notify with the schannel text on wss and mqtts

## Goal

wss and mqtts print `curl: (56) schannel: server closed abruptly (missing close_notify)` when the TLS peer closes without close_notify, as gophers and https do.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-026 (Low, confirmed: reproduced at least twice).

- Repro: TLS server (self-signed) that completes the WebSocket handshake (or sends MQTT CONNACK/SUBACK/PUBLISH) and closes without close_notify. `curl -sS -k --ssl-no-revoke wss://127.0.0.1:18500/chat`; `... mqtts://127.0.0.1:18500/a/b`.
- Expected: REF 8.22.0: exit 56, `curl: (56) schannel: server closed abruptly (missing close_notify)`.
- Actual: Exit 56, `curl: (56) Failure when receiving data from the peer`.
- Where to start: Neither file maps `MissingCloseNotifyException` to `TlsFailureMessages` as `GopherProtocolHandler` does.

## Acceptance criteria

- [ ] Both repros print `curl: (56) schannel: server closed abruptly (missing close_notify)` and exit 56; a unit test in each library pins it.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 61abeed2 a0b49a29.
