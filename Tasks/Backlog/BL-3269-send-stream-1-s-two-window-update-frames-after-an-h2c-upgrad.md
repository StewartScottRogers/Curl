---
id: BL-3269
title: Send stream 1's two WINDOW_UPDATE frames after an h2c upgrade as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitTests/Http2SessionTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3269 — Send stream 1's two WINDOW_UPDATE frames after an h2c upgrade as curl does

## Goal

After `--http2` upgrades an `http://` connection to h2c, Curl sends the same frames as curl 8.22.0 with nghttp2, including the two `WINDOW_UPDATE stream 1 increment 10420225` frames.

## Context

Found by BL-3207 (2026-10-23): `Record-CurlExchange.ps1 -Http2Origin h2c-upgrade` with Curl shows `SETTINGS 3=100 4=65536 2=0`, `WINDOW_UPDATE stream 0 increment 1048510465`, `SETTINGS ACK`, `GOAWAY last stream 0 error 0` but no `WINDOW_UPDATE stream 1` frames, which curl sends twice (`Documentation/Product/Http2OriginRecordings.md`). Prior knowledge and ALPN h2 already send them; the upgraded stream 1 is opened without a HEADERS frame in `Http2Session.cs`, so it likely skips the stream window update.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Http.UnitTests` pins the two stream-1 `WINDOW_UPDATE` frames after an h2c upgrade.
- [ ] Re-running `Record-CurlExchange.ps1 -Http2Origin h2c-upgrade` with Curl gives the recording's frames.
- [ ] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-23: Created.
