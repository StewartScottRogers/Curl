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
completed:
---
# BL-358 — Stamp -v lines with --trace-time

## Goal

`VerboseTransferEventWriter` prefixes each `-v` line that starts an event with the `--trace-time` stamp, `HH:MM:SS.uuuuuu `, read from an injected `TimeProvider`.

## Context

- Found in BL-229, which stamps the `--trace` and `--trace-ascii` dumps (`TraceTransferEventWriter.Timestamp`). curl 8.21.0 stamps `-v` lines too: `tool_debug_cb` in `src/tool_cb_dbg.c` writes `timebuf` before each `-v` prefix as well.
- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18294 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-s','-v','--trace-time','http://127.0.0.1:18294/f.txt','-o','NUL')` began standard error with `03:30:30.939000 *   Trying 127.0.0.1:18294...` and `03:30:30.940000 > GET /f.txt HTTP/1.1`. Measure the rest (continuation lines of a split header, data lines) before pinning.
- ADR-0046: the consumer stamps events from the injected clock when they arrive.

## Acceptance criteria

- [ ] A `-v --trace-time` HTTP exchange renders byte-equal to curl 8.21.0 (measured), with timestamps from an injected clock.
- [ ] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
