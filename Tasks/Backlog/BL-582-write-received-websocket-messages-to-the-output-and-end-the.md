---
id: BL-582
title: Write received WebSocket messages to the output and end the transfer as curl does
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-581]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-582 — Write received WebSocket messages to the output and end the transfer as curl does

## Goal

The payload of each received data frame reaches the transfer's output exactly as the curl 8.21.0 tool writes it, whatever it does with standard input or `-d` on a WebSocket (per BL-579's ADR) is done, and the transfer ends (server close, connection loss, `-m`) with curl's exit code, message, `%{size_download}` and progress.

## Context

- Conformance audit 2026-09-28, row 36. Builds on BL-581. Post-upgrade behaviour: BL-579's ADR and its measurements.
- Measure any case BL-579 did not: two text messages then close, a fragmented binary message, the server dropping the connection without a close frame, and `-w '%{size_download} %{http_code}'`.

## Acceptance criteria

- [ ] Measured first as above (where BL-579 did not); stdout bytes, stderr, exit code and `-w` values copied into Notes.
- [ ] `Curl.Protocol.Ws.UnitTests` pin output bytes, progress reports and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
