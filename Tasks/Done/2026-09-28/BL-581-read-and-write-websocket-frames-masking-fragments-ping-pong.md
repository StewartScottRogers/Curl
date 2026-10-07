---
id: BL-581
title: Read and write WebSocket frames: masking, fragments, ping, pong and close
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-580]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-581 — Read and write WebSocket frames: masking, fragments, ping, pong and close

## Goal

The WebSocket handler reads RFC 6455 frames (7-, 16- and 64-bit lengths, continuation fragments, text and binary), answers `ping` with a masked `pong` as curl 8.21.0 does, answers a server `close` as curl does, writes every client frame masked with a key from the injected random source, and treats a protocol violation (masked server frame, reserved bits, oversized control frame) as curl does.

## Context

- Conformance audit 2026-09-28, row 36. Builds on BL-580. Design: BL-579's ADR.
- Frames split across reads and several frames in one read must both work.
- Measure what curl does for a `ping` (does it send a `pong`, and when), a server `close` with and without a status code, and a masked server frame, with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` to catch curl's replies in `request.bin`).

## Acceptance criteria

- [x] Measured first as above; request bytes, stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ws.UnitTests` pin the frame reader for each length form and fragment case, the masked bytes of each client frame for a fixed key, and the measured behaviour for ping, close and each violation.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-28, curl 8.21.0 Schannel, `curl -s -S ws://127.0.0.1:<port>/`,
  `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500`, 38 cases. The request is always
  ADR-0128's upgrade head; what curl sent after it, stdout, stderr and exit code for every
  case are in the table in ADR-0131. In short:
  - ping `89 02 "hi"` → curl sends `8a 82 44 22 90 af 2c 4b` (masked pong echoing `hi`); empty
    ping → `8a 80 <mask>`; 125-byte ping → `8a fd <mask> <125 masked>`; two pings in one read →
    only one pong, for the last. Ping payloads are not written; exit 0.
  - close `88 02 03 e8`, `88 00`, `88 05 03 e8 "bye"`, `88 01 "x"` → nothing sent, payload
    written, reading goes on; exit 0. A server pong's payload is written too.
  - 16-bit, 64-bit and non-minimal lengths decode; a frame cut short writes what arrived (`hel`).
  - exit 56 with `[WS] masked input frame`, `invalid reserved bits: c1`, `invalid opcode: 83`,
    `no ongoing fragmented message to resume`, `fragmented message interrupted by new TEXT msg`
    (`BINARY`), `invalid fragmented PING frame` (`PONG`, `CLOSE`), `received PING frame is too big`
    (`PONG`, `CLOSE`), `frame length longer than 63 bits not supported`; payload decoded before
    the violation is written first.
- Built `WsOpcode`, `WsFrameEncoder` (FIN + masked, shortest length form),
  `WsFrameDecoder` (streaming, state kept across reads, violations in curl's order),
  `WsDecodedBytes` and `WsFrameReceiver` (reads until the server closes, hands payloads to a
  callback, sends the pong for the last ping of each read, returns frame bytes received).
  `WsProtocolHandler.SendAsync` became internal so the receiver shares its exit-55 handling.
- Scope choice: the receiver is not yet called from `WsProtocolHandler.ExecuteAsync`; wiring it
  in (output, exit 52 with no frames, `-m`, size, `-T`) is BL-582's goal, and doing it here
  would take that task's work. Decision record: ADR-0131 (decided by Claude under Stewart's
  delegation), which also records that no pong is sent for a read that ends in a violation.
- Tests: 152 in `Curl.Protocol.Ws.UnitTests` (65 new); `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Ws.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. WebSocket frames decode in every length form and fragment case, pings get curl's masked pong, violations fail with curl's 56 messages
