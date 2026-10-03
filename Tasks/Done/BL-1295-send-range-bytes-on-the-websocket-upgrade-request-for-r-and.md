---
id: BL-1295
title: Send Range: bytes= on the WebSocket upgrade request for -r and -C
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1294]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1295 — Send Range: bytes= on the WebSocket upgrade request for -r and -C

## Goal

The `ws://`/`wss://` upgrade request carries `Range: bytes=<text>` for `-r <text>` and `Range: bytes=<offset>-` for a non-zero `-C <offset>`, between `Host` and `User-Agent`, unless a `-H` header names `Range`, as curl 8.21.0 sends it.

## Context

- Today `Curl.Protocol.Ws.UnitLibrary/WsUpgradeRequestFormatter.cs` writes the request line, `Host`, then `User-Agent`, `Accept` and the upgrade headers, and never a `Range`; `ITransferContext.RangeText` and `ITransferContext.ResumeFrom` are not read.
- curl 8.21.0 (tag `curl-8_21_0`) builds the upgrade as an HTTP/1.1 GET: `lib/http.c` `http_range`, lines 2605-2625, sets `Range: bytes=%s\r\n` from `data->state.range` for a GET unless `Curl_checkheaders(data, "Range")`; `lib/url.c` `setup_range`, lines 1637-1660, makes `data->state.range` `<n>-` for a non-zero `-C <n>` (which wins over `-r`), or `-r`'s text, and sets no range for `-C 0` alone; the request writer adds it in the `H1_HD_RANGE` slot (`http.c` lines 2909-2911), after `Host` and before `User-Agent`.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: xxx\r\n\r\n\x81\x05hello\x88\x00'`:
  - `-sv -r 1-2 ws://127.0.0.1:PORT/` sends `GET / HTTP/1.1`, `Host: 127.0.0.1:PORT`, `Range: bytes=1-2`, `User-Agent: curl/8.21.0`, `Accept: */*`, `Upgrade: websocket`, `Sec-WebSocket-Version: 13`, `Sec-WebSocket-Key: ...`, `Connection: Upgrade`;
  - `-sv -C 5 ws://127.0.0.1:PORT/` sends the same with `Range: bytes=5-`.
  Each shows as a `> ` line under `-v`. Curl today sends neither; everything else in both requests matches apart from the random key.
- `-r` text `ByteRangeParser` refuses is stopped by `Curl.Console` before the handler, outside this task; use `RangeText` as typed.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Ws.UnitTests` assert the exact upgrade request bytes (with a fixed `IWebSocketRandomSource`) for the two measured runs.
- [x] Tests pin that `-C 0` alone sends no `Range`, that `-C 5` with `-r 1-2` sends `Range: bytes=5-`, and that a `-H 'range: x'` header (any letter case) suppresses curl's line and is sent in its own `-H` place.
- [x] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports no failing member.

## Notes

- 2026-10-03 (Claude, lane 3): `WsUpgradeRequestFormatter.RangeValue(resumeFrom, rangeText)` follows curl's `setup_range` (a non-zero `-C n` gives `n-` and wins over `-r`; otherwise the `-r` text as typed; `-C 0` alone gives none). `Format` takes the result as an optional `byteRange` and writes `Range: bytes=<value>` after `Authorization` and before `User-Agent`. Any `-H` header named `Range`, in any letter case, suppresses that line and is sent with the other `-H` headers. The range goes out whatever the method (`-X`, `-I`), because curl tests `httpreq`, which `-X` does not change. No ADR: this is the measured curl behaviour, not a new choice.
- Tests: `WsProtocolHandlerRangeTests` (the two measured runs plus the edge cases, exact bytes with `FixedRandomSource`) and `WsUpgradeRequestFormatterTests` (placement after `Authorization`, plus a `RangeValue` table). Ws fast tests: 314 pass, 1 skipped (it runs off Windows only). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary -SkipTestRun`, run on coverage from the Ws test project only (the full-solution run went past 30 minutes), reports 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ws/wss upgrade sends Range: bytes= for -r and non-zero -C, as curl 8.21.0 does
