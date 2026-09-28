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
completed:
---
# BL-581 — Read and write WebSocket frames: masking, fragments, ping, pong and close

## Goal

The WebSocket handler reads RFC 6455 frames (7-, 16- and 64-bit lengths, continuation fragments, text and binary), answers `ping` with a masked `pong` as curl 8.21.0 does, answers a server `close` as curl does, writes every client frame masked with a key from the injected random source, and treats a protocol violation (masked server frame, reserved bits, oversized control frame) as curl does.

## Context

- Conformance audit 2026-09-28, row 36. Builds on BL-580. Design: BL-579's ADR.
- Frames split across reads and several frames in one read must both work.
- Measure what curl does for a `ping` (does it send a `pong`, and when), a server `close` with and without a status code, and a masked server frame, with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` to catch curl's replies in `request.bin`).

## Acceptance criteria

- [ ] Measured first as above; request bytes, stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ws.UnitTests` pin the frame reader for each length form and fragment case, the masked bytes of each client frame for a fixed key, and the measured behaviour for ping, close and each violation.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
