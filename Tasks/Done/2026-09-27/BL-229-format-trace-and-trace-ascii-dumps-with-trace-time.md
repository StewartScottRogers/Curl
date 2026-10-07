---
id: BL-229
title: Format --trace and --trace-ascii dumps with --trace-time
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-163, BL-313]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-229 — Format --trace and --trace-ascii dumps with --trace-time

## Goal

Trace formatters render `--trace` and `--trace-ascii` dumps, with `--trace-time` prefixes, from BL-163's events.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Dumps for one HTTP exchange are byte-equal to curl 8.21.0 (measured), with timestamps from an injected clock.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered in-session rather than through the full `/feature` stages: ADR-0046 fixes the interface and the event boundaries, so the plan was a port of the trace branch of `tool_debug_cb` and of `dump` in curl's `src/tool_cb_dbg.c` behind `ITransferEvents`: `Curl.Output.UnitLibrary/TraceTransferEventWriter.cs` with `TraceDumpFormat` (`HexAndText` for `--trace`, `TextOnly` for `--trace-ascii`), tested by `TraceTransferEventWriterTests` (14 tests). The `Established connection`, `Reusing existing` and ALPN wording moved out of `VerboseTransferEventWriter` into `TransferEventInfoText` so `-v` and the dumps share one copy.
- Measured curl 8.21.0 (mingw, Schannel) on 2026-09-27, trace file byte for byte, pinned verbatim as fixtures in `Curl.Output.UnitTests/Fixtures` (a `.gitattributes` there marks `trace-*.txt` `-text` so the CR LFs survive checkout):
  - `Record-CurlExchange.ps1 -Port 18291 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-s','--trace',"$d\t.txt",'--trace-time','http://127.0.0.1:18291/f.txt','-o','NUL')` -> `trace-time-http.txt`.
  - The same on port 18292 with `--trace-ascii` -> `trace-ascii-time-http.txt`; on port 18293 with `--trace` and no `--trace-time` -> `trace-http.txt`.
  - Info lines are `* ` + text in 8.21.0 (not the older `== Info: `). The request head is one `=> Send header, 84 bytes (0x54)` dump, each response header line its own `<= Recv header` dump, the body `<= Recv data, 6 bytes (0x6)`. `--trace` is 16 bytes a line as `xx ` hex then text; `--trace-ascii` is up to 64 a line and ends a line at CR LF; bytes outside 0x20-0x7F show as `.`. Stamps are `HH:MM:SS.uuuuuu ` in local time. The file is text mode on Windows, so every line ends CR LF; the writer emits bare line feeds and the tests translate, as `Curl.Console`'s `LineFeedToCrLfStream` does for standard error.
- Decisions (sensible defaults, recorded here since ADR-0046 already fixes the design):
  - The stamp prints the injected clock's microseconds. The Windows build's measured stamps all end in `000` because its clock has millisecond resolution, not because curl formats milliseconds; the Linux and macOS builds print real microseconds. A script cannot depend on the digits, so the clock decides them.
  - `ReportTlsData` writes nothing, as `-v` does and as curl's Schannel build never emits `SSL data` dumps (ADR-0046's table).
  - Whether stamps are written is a constructor flag, `writesTimestamps`, beside the clock, so `Curl.Console` (BL-242) passes `TimeProvider.System` either way.
- Follow-up filed: BL-358, `-v --trace-time` stamps the `-v` lines too (measured, not in this task's scope).
- Verified: `dotnet build -warnaserror` clean, fast tests green solution-wide (Output 248 passed), `dotnet format --verify-no-changes` clean for both projects, `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TraceTransferEventWriter renders --trace and --trace-ascii dumps with --trace-time stamps from an injected clock, byte-equal to measured curl 8.21.0 for an HTTP exchange
