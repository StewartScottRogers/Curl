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
completed: 2026-10-02
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

- [x] curl remeasured; request bytes for both cases copied into Notes.
- [x] `Curl.Protocol.Http.UnitTests` pin the whole written byte sequence after an upgraded `200` and after an upgraded `401` retried on stream 3.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measurement (2026-10-02)

curl.se's nghttp2 curl 8.18.0 (`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`),
`Record-CurlExchange.ps1 -Curl <that> -HoldOpenMilliseconds 1500`, server answering `101`,
an empty SETTINGS (never acknowledging curl's), then stream 1's response. Bytes after the
HTTP/1.1 upgrade request; every case starts with the preface
`PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`, `000012 04 00 00000000 0003 00000064 0004 00010000 0002 00000000`
(SETTINGS), `000004 08 00 00000000 3e7f0001` (WINDOW_UPDATE 0), `000000 04 01 00000000` (SETTINGS ACK).

- **One URL, 200** (`--http2 -v http://127.0.0.1:48970/a`, HEADERS `88`, DATA `hi\n` END_STREAM):
  then only `000011 07 00 00000000 00000000 00000000 73687574646f776e00` (GOAWAY "shutdown"). No extra SETTINGS.
- **Two URLs, 200** (`... /a http://127.0.0.1:48972/b`): then
  `000006 04 00 00000000 0004 00010000` (SETTINGS INITIAL_WINDOW_SIZE 65536),
  `000022 01 05 00000003 8286 418b089d5c0b8170dc69e7dd17 04022f62 7a8825b650c3cb85e5c1 53032a2f2a` (HEADERS stream 3),
  `000004 08 00 00000003 009f0001` twice.
- **Three URLs** (stream 3 and 5 answered): the SETTINGS goes out once, before stream 3's
  HEADERS only; stream 5's HEADERS `000009 01 05 00000005 8286c004022f63bfbe` follows directly.
- **401 retry** (`--http2 -v --anyauth -u a:b http://127.0.0.1:48973/a`, HEADERS `:status 401`,
  `www-authenticate: Basic realm="x"`, DATA `no\n` END_STREAM, all in the first read): then
  `000004 03 00 00000001 00000005` (RST_STREAM stream 1, STREAM_CLOSED), the same SETTINGS,
  `00002d 01 05 00000003 8286 418b089d5c0b8170dc69e7dd67 04022f61 1f0888ba34188a73df59bf 7a8825b650c3cb85e5c1 53032a2f2a`
  (HEADERS stream 3 with `authorization: Basic YTpi` never indexed), `000004 08 00 00000003 009f0001` twice.
  stderr shows `Ignoring the response-body` right before; the reset is tied to curl giving up
  the stream with its body unread, even though the DATA had already arrived.

### Decisions

- The RST_STREAM applies to every HTTP/2 stream whose body is discarded (retry or followed
  redirect), not just upgraded ones, and the discarded body is no longer read: ADR-0342.
  HTTP/3 is unchanged (unmeasured).
- The post-upgrade SETTINGS is written by `Http2Session` straight to the connection before
  the first stream it opens after `StartUpgradedStreamAsync`; `Curl.Http2.UnitLibrary` (not
  in `touches`) is left alone.
- The two h2c handler tests now replay the recorded server frames exactly (no server SETTINGS
  ACK, via `RecordedUpgradeFrames`), so stream 3's WINDOW_UPDATE increment is curl's
  `009f0001`, and the authority port is the measured one (48866, 48973) so the HEADERS bytes
  are curl's. The single-URL test now pins its whole written sequence too.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. An h2c-upgraded connection sends curl's post-upgrade SETTINGS before stream 3 and resets an ignored stream-1 body with STREAM_CLOSED, byte for byte
