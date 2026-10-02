---
id: BL-970
title: Send curl's post-upgrade SETTINGS and the RST_STREAM for an ignored stream-1 body after an h2c upgrade
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-866]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-970 — Send curl's post-upgrade SETTINGS and the RST_STREAM for an ignored stream-1 body after an h2c upgrade

## Goal

After a `--http2` h2c upgrade, the frames Curl writes on the connection match curl.se's nghttp2 curl 8.18.0 byte for byte: the extra SETTINGS it sends after its preface, and the RST_STREAM it sends on stream 1 when it ignores that stream's body before a retry.

## Context

- Measured in BL-866's Notes (`Tasks/Done/.../BL-866-*.md`) with `Record-CurlExchange.ps1 -Curl %LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe -HoldOpenMilliseconds 1500`, server sending `101`, an empty SETTINGS, then stream 1's response:
  - After the preface, the connection WINDOW_UPDATE and the SETTINGS ACK, curl sends a second SETTINGS frame, `000006 04 00 00000000 0004 00010000` (INITIAL_WINDOW_SIZE 65536). Curl does not send it today (`Http2Session.StartUpgradedStreamAsync`).
  - With `--anyauth -u a:b` and a `401` plus a 3-byte DATA on stream 1, curl sends `RST_STREAM` on stream 1 with STREAM_CLOSED (`000004 03 00 00000001 00000005`) right after the SETTINGS ACK and before that SETTINGS, then HEADERS on stream 3. With a `200` whose body is delivered it sends no RST_STREAM.
- Measure again first to see exactly when the RST_STREAM goes out (it may be tied to "Ignoring the response-body").

## Acceptance criteria

- [ ] curl remeasured; request bytes for both cases copied into Notes.
- [ ] `Curl.Protocol.Http.UnitTests` pin the whole written byte sequence after an upgraded `200` and after an upgraded `401` retried on stream 3.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
