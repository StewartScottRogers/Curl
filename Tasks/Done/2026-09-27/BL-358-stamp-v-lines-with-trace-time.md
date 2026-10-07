---
id: BL-358
title: Stamp -v lines with --trace-time
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-229]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-358 — Stamp -v lines with --trace-time

## Goal

`VerboseTransferEventWriter` prefixes each `-v` line that starts an event with the `--trace-time` stamp, `HH:MM:SS.uuuuuu `, read from an injected `TimeProvider`.

## Context

- Found in BL-229, which stamps the `--trace` and `--trace-ascii` dumps (`TraceTransferEventWriter.Timestamp`). curl 8.21.0 stamps `-v` lines too: `tool_debug_cb` in `src/tool_cb_dbg.c` writes `timebuf` before each `-v` prefix as well.
- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18294 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-s','-v','--trace-time','http://127.0.0.1:18294/f.txt','-o','NUL')` began standard error with `03:30:30.939000 *   Trying 127.0.0.1:18294...` and `03:30:30.940000 > GET /f.txt HTTP/1.1`. Measure the rest (continuation lines of a split header, data lines) before pinning.
- ADR-0046: the consumer stamps events from the injected clock when they arrive.

## Acceptance criteria

- [x] A `-v --trace-time` HTTP exchange renders byte-equal to curl 8.21.0 (measured), with timestamps from an injected clock.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18358 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-s','-v','--trace-time','-d','abc','http://127.0.0.1:18358/f.txt','-o','NUL')`. Every line start carries the stamp in front of its prefix: `* ` info, each `> ` request header line, each `< ` response header line, and the `} [3 bytes data]` / `{ [6 bytes data]` data lines. Pinned byte for byte in `VerboseTransferEventWriterTests.HttpExchangeWithTraceTime_StampsEachLineStartAsCurl`.
- Continuation of an open line (a header split across callbacks) is not measurable with a canned loopback server; it follows curl's source (`tool_debug_cb` calls `log_line_start`, which writes `timebuf` then the prefix, only when `!newl`), so it gets neither stamp nor prefix. curl fills `timebuf` once per callback, so all lines of one request header event share one clock reading; the writer does the same.
- Design: `VerboseTransferEventWriter` gains a primary constructor `(output, writesDataLines, writesTimestamps, timeProvider, tlsBackend)`, mirroring `TraceTransferEventWriter`; the existing `(output, writesDataLines)` and `(output, writesDataLines, tlsBackend)` constructors keep working and write no stamps, so BL-242 (in Doing on another lane) is not broken. The stamp format moved into a shared internal `TraceTimeStamp.Read`, used by both writers. No ADR: there was no choice to make beyond matching curl (ADR-0046 already covers the injected clock).
- Wiring `--trace-time` into `-v` in `Curl.Console` belongs to BL-242, whose goal already names `--trace-time`; it should use the new four-argument constructor. No follow-up filed.
- `Measure-CodeQuality.ps1` also reports one failing member outside this task's touches: `Curl.Console` `DiskWriteOutFileOpener.TryOpen` at 0% in the fast run. Pre-existing, not touched here.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v --trace-time stamps each line start as curl 8.21.0 does, from an injected TimeProvider
