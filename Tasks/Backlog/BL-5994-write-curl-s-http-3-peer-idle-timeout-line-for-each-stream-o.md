---
id: BL-5994
title: Write curl's HTTP/3 peer idle timeout line for each stream opened on an idle connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5982]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http3Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamTrace.cs, Curl.Original.Protocol.Http.UnitTests/Http3PeerIdleTimeoutTraceTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5994 — Write curl's HTTP/3 peer idle timeout line for each stream opened on an idle connection

## Goal

Under `--trace-config http/3`, Curl writes `[HTTP/3] peer idle timeout is <n>ms, set keep-alive to <n/2> ms.` each time curl does, including a second URL on a reused connection.

## Context

Measured during BL-5982 with curl 8.18.0 against `Record-CurlExchange.ps1 -Http3StalledOrigin`, `--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y <url>/z`: curl writes the line before `[<id>] OPENED stream` for streams 0, 4 and 8. Curl writes it only as the connection's first stream opens (`Http3Session.OpenRequestStreamAsync`, ADR-0388). curl 8.18.0 `cf-ngtcp2.c` `cf_ngtcp2_setup_keep_alive` sets it per stream; measure what it writes when another stream is already active.

## Acceptance criteria

- [ ] The Notes record when curl writes the line for a second stream on a connection.
- [ ] Curl writes it there, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
