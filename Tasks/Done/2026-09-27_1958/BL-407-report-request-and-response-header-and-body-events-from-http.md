---
id: BL-407
title: Report request and response header and body events from HttpProtocolHandler
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0046-the-transfer-context-carries-a-transfer-event-sink-for-v-and-trace.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-407 — Report request and response header and body events from HttpProtocolHandler

## Goal

`HttpProtocolHandler` reports the request head, each response header line, the body bytes sent and received, and curl's `using HTTP/1.x` and `Request completely sent off` info lines to `context.Events`, so `-v` and `--trace` show the exchange.

## Context

- ADR-0046 fixes the event kinds and boundaries: one `ReportRequestHeader` per head write, one `ReportResponseHeader` per received line (status line and final blank line included), one `ReportDataReceived` per body read, one `ReportDataSent` per body write.
- Today the handler reports only its connection-end lines (`Connection #0 to host ... left intact` and the like), so `curl -v` over this handler prints almost nothing.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18441 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-s','-v','http://127.0.0.1:18441/f.txt','-o','o')`. The bytes are pinned in `Curl.Console.UnitTests/CurlCommandRunnerTransferEventTests.cs` (BL-242), where a scripted handler reports the events this task makes the real one report.

## Acceptance criteria

- [x] A test over a scripted connection records, in order, the events the measured `-s -v` exchange shows after the connect: `using HTTP/1.x`, one 84-byte request head, `Request completely sent off`, four response header lines, one 6-byte data event, then the `left intact` line.
- [x] With `-d hi`, a 2-byte `ReportDataSent` follows the head (curl prints `} [2 bytes data]`, ADR-0046).
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.
- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`:
  - `-s --trace-ascii - http://127.0.0.1:18471/c` against `Transfer-Encoding: chunked`, `Trailer: X-T`, body `3 abc 0 X-T: 1`: one `Recv data, 21 bytes` holding the raw chunk framing and the trailer, and no `Recv header` for the trailer. So `ReportDataReceived` carries bytes as received, before any decoding.
  - `-s -L -v http://127.0.0.1:18472/a` against a 302 with a 4-byte body: headers reported, no data line for the ignored body; then `Ignoring the response-body` / `setting size while ignoring` lines (left to BL-449).
  - `-s -v -d hi http://127.0.0.1:18473/p`: `} [2 bytes data]`, then `* upload completely sent off: 2 bytes` in place of `Request completely sent off`.
  - `printf abcde | curl -s --trace-ascii - -T - http://127.0.0.1:18475/u`: `Send data, 10 bytes` (`5\r\nabcde\r\n`), `Send data, 5 bytes` (`0\r\n\r\n`), `upload completely sent off: 15 bytes` - framing included in the data events and the count. `Done waiting for 100-continue` left to BL-449.
  - `-s -v` with two URLs on one connection: `using HTTP/1.x` only after a fresh connect, never on the reused connection.
- Implementation: `HttpRequestBodyWriter.Events` reports the head once when it is written and each framed piece plus the closing chunk; `HttpResponseHeadReader.Events` reports each accepted line (1xx heads included); `HttpResponseBodyReader.Events` reports the head's body prefix and each raw read; the handler reports `using HTTP/1.x` on a new connection and the sent-off line after the send, and gives a discarded body no event sink.
- Choice (sensible default): a body a final status stopped (417 or 3xx while sending) gets no sent-off line, since the body was not completely sent. Not measured; BL-449 measures the lines around the 100-continue wait and can correct it.
- ADR-0046's `ReportDataSent` and `ReportDataReceived` rows amended with the measured boundaries; the ADR file was added to `touches` (no task in Doing names it).
- `RecordingTransferEvents` now records every header and data event as well as the info lines; the reuse tests' expected info lines gained the measured `using HTTP/1.x` and `Request completely sent off`.
- `Measure-CodeQuality.ps1`: `Curl.Protocol.Http.UnitLibrary` 100% line, 100% branch, 0 failing. The one failing member solution-wide, `Curl.Console` `DiskWriteOutFileOpener.TryOpen`, predates this task and is BL-432.
- Follow-up filed: BL-449 (the remaining info lines).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HttpProtocolHandler reports the request head, each response header line, body data sent and received, using HTTP/1.x and the sent-off line to context.Events, so -v and --trace show the exchange
