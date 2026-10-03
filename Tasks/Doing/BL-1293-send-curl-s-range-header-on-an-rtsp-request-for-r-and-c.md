---
id: BL-1293
title: Send curl's Range header on an RTSP request for -r and -C
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1292]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1293 — Send curl's Range header on an RTSP request for -r and -C

## Goal

An `rtsp://` request carries `Range: <text>` for `-r <text>`, and `Range: <offset>-` for `-C <offset>`, placed after `CSeq` (and `Session`) and before `Referer` and `User-Agent`, unless a `-H` header names `Range`, as curl 8.21.0 sends it.

## Context

- Today `Curl.Protocol.Rtsp.UnitLibrary/RtspRequestFormatter.cs` writes the request line, `CSeq`, `Session`, `Referer`, `User-Agent`, `Authorization`, then the `-H` headers, and never a `Range`; `ITransferContext.RangeText` and `ITransferContext.ResumeFrom` are not read.
- curl 8.21.0 `lib/rtsp.c` (tag `curl-8_21_0`):
  - lines 457-473: `if(data->state.use_range && (rtspreq & (RTSPREQ_PLAY | RTSPREQ_PAUSE | RTSPREQ_RECORD)))` then, unless `Curl_checkheaders(data, "Range")`, `p_range = "Range: %s\r\n"` of `data->state.range`. The comment says PLAY, PAUSE and RECORD only, but `RTSPREQ_*` is a plain enum (`lib/urldata.h` line 489 on: OPTIONS 1 ... PLAY 5, PAUSE 6, RECORD 10), so the mask is 15 and every request the curl tool makes (OPTIONS) sends it.
  - lines 492-525: the request is the request line, `CSeq`, `Session` (when held), then `Transport`, `Accept`, `Accept-Encoding`, `Range`, `Referer`, `User-Agent`, then the custom headers.
  - `lib/url.c` `setup_range`, lines 1637-1660: a non-zero `-C <n>` sets `data->state.range` to `<n>-` and wins over `-r`; otherwise `-r`'s text is used verbatim (no `bytes=`); `-C 0` with no `-r` sets no range at all.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 0\r\n\r\n'`:
  - `-sv -r 1-2 rtsp://127.0.0.1:PORT/` sends `OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 1-2\r\nUser-Agent: curl/8.21.0\r\n\r\n`;
  - `-sv -C 5 rtsp://127.0.0.1:PORT/` sends `... CSeq: 1\r\nRange: 5-\r\nUser-Agent: curl/8.21.0\r\n\r\n`;
  - `-sv -r 1-2 -H 'Range: npt=0-' rtsp://127.0.0.1:PORT/` sends no `Range: 1-2`, only `Range: npt=0-` among the `-H` headers after `User-Agent` (Curl already matches this one).
  Each `Range` line also shows as a `> ` header line under `-v`. Curl today sends no `Range` for the first two.
- `-r` text that `ByteRangeParser` refuses is stopped by `Curl.Console` before the handler runs; that is outside this task. Use `RangeText` (the text as typed) for `-r`.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Rtsp.UnitTests` (`RtspRequestFormatterTests` or the handler tests) assert the exact request bytes of the three measured runs, including `-e http://r/` with `-r 1-2` placing `Range: 1-2` before `Referer: http://r/`.
- [ ] Tests pin that `-C 0` alone sends no `Range`, that `-C 5 -r 1-2` sends `Range: 5-`, and that a held session ID's `Session` line comes before `Range`.
- [ ] A test pins that a `-H Range` header of any letter case suppresses curl's `Range` line.
- [ ] `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Rtsp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports no failing member.

## Notes

- `-C -` (resume from the output's size) needs an output to measure; pin only the numeric forms here.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
