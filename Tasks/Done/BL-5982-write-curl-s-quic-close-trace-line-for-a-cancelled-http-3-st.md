---
id: BL-5982
title: Write curl's quic close trace line for a cancelled HTTP/3 stream
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5981]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamTrace.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3Session.cs, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5982 — Write curl's quic close trace line for a cancelled HTTP/3 stream

## Goal

Under `--trace-config http/3`, Curl writes `[HTTP/3] [<id>] quic close(app_error=268) -> 0` for a stream a time limit cancelled, where curl writes it.

## Context

ADR-1410 (BL-5978): curl 8.18.0 writes the line during the next transfer on the connection, once QUIC closes the cancelled stream. `Http3StreamConnection.CancelUnlessEndedBothWaysAsync` cancels it; `Http3StreamTrace` writes the other lines. Measure with `Record-CurlExchange.ps1 -Http3StalledOrigin` (BL-5981).

## Acceptance criteria

- [x] The Notes record where curl writes the line among the next transfer's lines.
- [x] Curl writes the line there, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured with curl 8.18.0 (WinGet, ngtcp2 1.21.0), `Record-CurlExchange.ps1 -Http3StalledOrigin -Port 18443`,
`--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y <url>/z`, four runs:

- `[HTTP/3] [0] quic close(app_error=268) -> 0` comes during `/y`, in all four runs after
  `* Request completely sent off` and before `< HTTP/3 200`.
- `[HTTP/3] [4] quic close(...)` comes during `/z`, in some runs before `[8] OPENED stream`:
  it depends on when the server's answer to STOP_SENDING arrives.

Curl writes it at the next read of a response on the connection (ADR-1412). `touches` kept; the
ADR is the only other file. `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`:
0 failing members. Fast tests: 58380 passed, 0 failed, 363 skipped. Filed BL-5994 (curl also writes
`peer idle timeout` for a reused connection's later streams).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 8375b780 c3b70efa 3d836d5e.
- 2026-11-25: Doing -> Done. Curl writes the cancelled HTTP/3 stream's quic close(app_error=268) trace line during the next transfer, before its response head
