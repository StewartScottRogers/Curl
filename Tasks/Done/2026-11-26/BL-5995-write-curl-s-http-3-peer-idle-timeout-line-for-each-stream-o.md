---
id: BL-5995
title: Write curl's HTTP/3 peer idle timeout line for each stream opened on an idle connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5982]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http3Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamTrace.cs, Curl.Original.Protocol.Http.UnitTests/Http3PeerIdleTimeoutTraceTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5995 — Write curl's HTTP/3 peer idle timeout line for each stream opened on an idle connection

## Goal

Under `--trace-config http/3`, Curl writes `[HTTP/3] peer idle timeout is <n>ms, set keep-alive to <n/2> ms.` each time curl does, including a second URL on a reused connection.

## Context

Measured during BL-5982 with curl 8.18.0 against `Record-CurlExchange.ps1 -Http3StalledOrigin`, `--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y <url>/z`: curl writes the line before `[<id>] OPENED stream` for streams 0, 4 and 8. Curl writes it only as the connection's first stream opens (`Http3Session.OpenRequestStreamAsync`, ADR-0388). curl 8.18.0 `cf-ngtcp2.c` `cf_ngtcp2_setup_keep_alive` sets it per stream; measure what it writes when another stream is already active.

## Acceptance criteria

- [x] The Notes record when curl writes the line for a second stream on a connection.
- [x] Curl writes it there, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured with curl 8.18.0 (WinGet, ngtcp2), `Record-CurlExchange.ps1 -Http3StalledOrigin`,
`--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y <url>/z`:

- One after another (BL-5982): the line comes before `[<id>] OPENED stream` for streams 0, 4 and 8,
  each opened after the earlier transfer was done.
- With `-Z` added: the line comes once, before `[0] OPENED stream`. `[4]` and `[8] OPENED stream`,
  opened while earlier streams were active, have none.

So curl writes it for a second stream only when the connection has no active stream (ADR-1413).
`ConnectionReady` is renamed `OpeningStreamOnIdleConnection` for what it now says. `touches` kept;
the ADR and its README row are the only other files. A transfer that fails other than by timing out
is still not marked done (ADR-0388), so no line follows it: filed BL-5996.
`Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.
`dotnet build` 0 warnings, fast tests green (Http: 2898 passed, 0 failed, 44 skipped).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: cd3330ad 870d61b4 3d836d5e.
- 2026-11-25: Doing -> Done. Curl writes the HTTP/3 peer idle timeout line for each stream opened on an idle connection
