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
completed: 2026-09-28
---
# BL-580 — Open a WebSocket with curl's HTTP/1.1 upgrade request and check the 101 reply

## Goal

A `WsProtocolHandler` in `Curl.Protocol.Ws.UnitLibrary` connects through `IConnector` (TLS for `wss://`), sends the upgrade request byte for byte as curl 8.21.0 sends it (header order, `Sec-WebSocket-Version: 13`, a base64 16-byte key from the injected random source), reads the reply head, checks `101`, `Upgrade`, `Connection` and `Sec-WebSocket-Accept` (SHA-1 per RFC 6455), and fails a refused or wrong upgrade with curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 36 (Blocker; WS split: this handshake, BL-581 frames, BL-582 output and end, BL-583 registration, BL-584 `-v`). Design: BL-579's ADR, including where the request writer and head reader live and which HTTP options apply.
- `SHA1` from `System.Security.Cryptography`; tests inject the key.
- Use BL-579's measurements; add any missing case with `Record-CurlExchange.ps1` (a `200`, a `101` with a wrong accept value, a `101` without `Upgrade`, a `401`).

## Acceptance criteria

- [x] `Curl.Protocol.Ws.UnitTests` pin the request bytes for a fixed key (with and without `-H` and `-u` if BL-579's ADR applies them here) and the outcome for each measured reply, through a fake `IConnector`/`IConnection`.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built per ADR-0128: `WsUpgradeRequestFormatter` (curl's header order, `-H`/`-A`/`-e`/`-X`,
  `Connection` merge), `WsUpgradeResponseReader` + `WsStatusLine` (head up to the blank line,
  bytes after it kept in `WsUpgradeResponse.Remaining` for BL-581), `IWebSocketRandomSource` /
  `SystemWebSocketRandomSource`, `WsCustomHeader` (copy of the HTTP library's `-H` rules; no
  cross-protocol reference), `WsIoFailures`, and `WsProtocolHandler` (connect with TLS for `wss`,
  proxy handed to the connector, pre-emptive `Authorization` from `IHttpAuthenticator` with no
  challenges, `-D` gets the head, anything but `101` → 22 `Refused WebSocket upgrade: <code>`).
- The goal's "check `Upgrade`, `Connection` and `Sec-WebSocket-Accept`" is replaced by ADR-0128:
  curl 8.21.0 does not check them (rows 21, 22), so neither do we; tests pin that.
- Extra measurements (curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, 2026-09-28):
  `-D -` on a `200` refusal writes the head, not the body, then exit 22; `garbage` → 1
  `Received HTTP/0.9 when not allowed`; `HTTP/2 101`/`HTTP/3 101` → 1 `Unsupported HTTP version
  (2.0|3.0) in response`; `HTTP/9` → 1 `Unsupported HTTP version in response`; `HTTP/1.2` and
  `HTTP/1.1 1x1` → 1 `Unsupported HTTP/1 subversion in response`; `HTTP/1.1 099` → 1
  `Unsupported response code in HTTP response`; `http/1.1 101` → treated as 200, exit 22;
  `HTTP/1.0 101` and a bare `HTTP/1.1 101` are accepted; no reply, or a head cut off, → 52
  `Empty reply from server`; a `101` followed by an immediate close → 52 (noted on BL-582).
- Defaults taken: send/flush `IOException` → 55 and read `IOException` → 56 with the HTTP
  library's messages; a failed `-D` write → 23 `Failure writing output to destination, passed n
  returned m`; the reply head is capped at 102400 bytes (exit 100, the HTTP library's line
  limit), exact because no read goes past the cap. Not separately measured; they mirror the
  HTTP library so the two protocols fail alike.
- `%{http_code}` for a refused `HTTP/1.1 099` shows `099` in curl; the report here carries no
  code on an exit-1 status-line failure. Left to BL-583/BL-584, which own `-w` wiring.
- The upgrade succeeds with 0 bytes and `Report.ResponseCode = 101`; frames (BL-581) and output
  and end of transfer (BL-582) are not part of this task.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. WsProtocolHandler sends curl's WebSocket upgrade request byte for byte and accepts a 101, refusing anything else with exit 22
