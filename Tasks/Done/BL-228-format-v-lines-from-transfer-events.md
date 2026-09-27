---
id: BL-228
title: Format -v lines from transfer events
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-163, BL-313]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-228 — Format -v lines from transfer events

## Goal

A verbose formatter renders `* `, `> ` and `< ` lines from BL-163's transfer events.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] One HTTP and one HTTPS exchange render line for line as curl 8.21.0 `-v` (measured).
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered in-session rather than through the full `/feature` stages: ADR-0046 names the interface and the wording each event renders as, so the plan was a port of the `-v` branch of `tool_debug_cb` (curl `src/tool_cb_dbg.c`) behind `ITransferEvents`, in one new class, `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, tested by `VerboseTransferEventWriterTests` (16 tests).
- Measured curl 8.21.0 (mingw, Schannel) on 2026-09-27, standard error byte for byte:
  - HTTP: `Record-CurlExchange.ps1 -Port 18228 -Response 'HTTP/1.1 200 OK
Content-Type: text/plain
Content-Length: 6

hello
' -CurlArgs @('-s','-v','http://127.0.0.1:18228/f.txt','-o','NUL')` - pinned verbatim in `HttpExchange_RendersAsCurl` (local port 54485).
  - HTTPS: a throwaway C# `SslStream` loopback server on 127.0.0.1:18229 (self-signed, answering `HTTP/1.1 200 OK`, `Content-Length: 6`, `hello
`) and `curl -s -v -k https://127.0.0.1:18229/f.txt -o NUL`: with server ALPN `http/1.1` it printed `* ALPN: curl offers http/1.1` then `* ALPN: server accepted http/1.1` - pinned verbatim in `HttpsExchange_RendersAsCurl`; with no server ALPN `* ALPN: server did not agree on a protocol. Uses default.`; with `--no-alpn` no ALPN line at all. On HTTPS the `Established connection` line comes after the ALPN lines, so the connector must report `ConnectionOpened` after the handshake (for whoever wires the reporting).
  - A 200000-byte body printed one `{ [102357 bytes data]` line: consecutive data events collapse to one line with the first read's count, as `traced_data` does in curl's source.
  - Lines are CRLF on info lines and CR CR LF on header lines because curl's standard error is text mode on Windows; the writer emits bare line feeds (curl's `fwrite` bytes) and the tests translate LF to CRLF, as `Curl.Console`'s `LineFeedToCrLfStream` does.
- Decisions (sensible defaults, recorded here rather than in an ADR since ADR-0046 already fixes the design): whether data lines print is a constructor flag, `writesDataLines`, because curl prints them only when standard output is not a terminal (or `-v` output goes elsewhere) and only `Curl.Console` (BL-242) knows that; TLS facts are worded as the Schannel build only, since the OpenSSL build could not be measured here - filed BL-354 for Linux and macOS; `ReportTlsData` prints nothing, per ADR-0046's table. The `schannel:` lines, `Trying`, `using HTTP/1.x`, `Request completely sent off` and `left intact` are `ReportInfo` text from their reporters, not this writer.
- Verified: `dotnet build -warnaserror` clean, fast tests green solution-wide (Output 234 passed), `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. VerboseTransferEventWriter renders -v lines from transfer events, byte-equal to measured curl 8.21.0 for an HTTP and an HTTPS exchange
