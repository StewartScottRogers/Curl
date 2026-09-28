---
id: BL-580
title: Open a WebSocket with curl's HTTP/1.1 upgrade request and check the 101 reply
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-579]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-580 — Open a WebSocket with curl's HTTP/1.1 upgrade request and check the 101 reply

## Goal

A `WsProtocolHandler` in `Curl.Protocol.Ws.UnitLibrary` connects through `IConnector` (TLS for `wss://`), sends the upgrade request byte for byte as curl 8.21.0 sends it (header order, `Sec-WebSocket-Version: 13`, a base64 16-byte key from the injected random source), reads the reply head, checks `101`, `Upgrade`, `Connection` and `Sec-WebSocket-Accept` (SHA-1 per RFC 6455), and fails a refused or wrong upgrade with curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 36 (Blocker; WS split: this handshake, BL-581 frames, BL-582 output and end, BL-583 registration, BL-584 `-v`). Design: BL-579's ADR, including where the request writer and head reader live and which HTTP options apply.
- `SHA1` from `System.Security.Cryptography`; tests inject the key.
- Use BL-579's measurements; add any missing case with `Record-CurlExchange.ps1` (a `200`, a `101` with a wrong accept value, a `101` without `Upgrade`, a `401`).

## Acceptance criteria

- [ ] `Curl.Protocol.Ws.UnitTests` pin the request bytes for a fixed key (with and without `-H` and `-u` if BL-579's ADR applies them here) and the outcome for each measured reply, through a fake `IConnector`/`IConnection`.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
