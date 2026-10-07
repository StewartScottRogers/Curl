---
id: BL-788
title: Send HEAD and end with exit 52 for -I on a WebSocket URL
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-583]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-788 — Send HEAD and end with exit 52 for -I on a WebSocket URL

## Goal

`curl -I ws://...` sends `HEAD` instead of `GET` in the upgrade request and, after a `101`, reads no frames and ends with exit 52 `Empty reply from server`, as curl 8.21.0 does.

## Context

- Measured in BL-583 (curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, 2026-09-28): `curl -sS -I -w '%{http_code} %{size_download} %{size_header} %{size_request}' ws://127.0.0.1:47911/` against a 102-byte `101` head followed by `81 05 "hello"` `88 02 03 e8` sent `HEAD / HTTP/1.1` with the usual upgrade headers (193 bytes), wrote nothing but the `-w` text `101 0 102 193`, and exited 52 with `curl: (52) Empty reply from server`. The head is not written to stdout (BL-583 already routes `-i`/`-I` heads only to `-D` for WebSocket URLs).
- `ITransferContext.NoBody` carries `-I`; `WsProtocolHandler` (`Curl.Protocol.Ws.UnitLibrary`) picks the method from `HttpRequestOptions.CustomMethod ?? "GET"`. ADR-0128.

## Acceptance criteria

- [x] A `WsProtocolHandlerTests` test with `NoBody = true` pins the `HEAD` request bytes, no payload written, `Report.DownloadSize` 0, `RequestSize` of the request, `ResponseCode` 101 and exit 52 `Empty reply from server`, with frames after the head left unread.
- [x] `-X` given with `-I` keeps the `-X` method (check against curl 8.21.0 first and pin what it does).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered in-session rather than through the full `/feature` agent chain: the change is one method choice and one early return in `WsProtocolHandler`, fully specified by the measurements below.
- Measured 2026-09-29, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, 101 head + `81 05 "hello" 88 02 03 e8`:
  - `-sS -I -X POST -w '%{http_code} %{size_download} %{size_header} %{size_request} %{method}'` sent `POST / HTTP/1.1` with the upgrade headers, printed `101 0 77 193 POST`, exited 52 `Empty reply from server`. So `-X` wins over `-I`'s `HEAD`, pinned by `ExecuteAsync_NoBodyWithCustomMethod_KeepsTheCustomMethod`.
  - `-sS -v -I`: after the two `Received 101` lines, `{ [11 bytes data]`, `* Empty reply from server`, `* shutting down connection #0`. The frame bytes that arrived with the head are reported as one read but not decoded or written; nothing more is read. Pinned by `WsProtocolHandlerEventTests.ExecuteAsync_NoBody_ReportsTheFramesWithTheHeadAsOneReadThenEmptyReply`.
- Choice: when no bytes arrived with the head, no read event is reported and no further read is made (curl stops reading after the head for `-I`; the empty-read case was not separately observable with the loopback server, which sends head and frames in one write). No ADR: this follows measured behaviour and ADR-0128's model, no new design decision.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. curl -I ws:// sends HEAD (or the -X method), reads no frames and exits 52 Empty reply from server, as curl 8.21.0 does
