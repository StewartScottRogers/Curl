---
id: BL-1216
title: Print Request completely sent off for an empty -d body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1216 — Print Request completely sent off for an empty -d body

## Goal

`curl -v -d '' <url>` prints `Request completely sent off`, as curl 8.21.0 does, not `upload completely sent off: 0 bytes`.

## Context

- Found in BL-1189, measured 2026-10-02: real curl 8.21.0 `-v --trace-config read -d ''` writes no reader lines and `Request completely sent off`; Curl writes `upload completely sent off: 0 bytes` (`HttpProtocolHandler.ReportRequestSent`).

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1` for `-d ''` and `-T` of an empty file; stderr in Notes.
- [x] Tests pin each case's line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02 with `Record-CurlExchange.ps1` against curl 8.21.0 (Schannel):
  - `-s -v -d '' http://127.0.0.1:18216/p`: head with `Content-Length: 0` and
    `Content-Type: application/x-www-form-urlencoded`, no `}` data line, then
    `* Request completely sent off`, then the response.
  - `-s -v -T empty.txt http://127.0.0.1:18217/u`: `PUT` head with `Content-Length: 0`, no
    data line, then `* Request completely sent off`.
  - Contrast, `-s -v -T -` with empty stdin: chunked, `} [5 bytes data]` (the closing chunk)
    and `* upload completely sent off: 5 bytes`. So the rule is the bytes on the wire, not
    the body's length.
- Fix: `HttpProtocolHandler.ReportRequestSent` writes `Request completely sent off` when a
  whole body put 0 bytes on the wire (`HttpRequestBodyWriter.BytesSent == 0`). A body a final
  status stopped still reports nothing.
- Tests: `HttpProtocolHandlerTests.ExecuteAsync_PostWithEmptyData_ReportsRequestCompletelySentOff`
  and `ExecuteAsync_UploadOfAnEmptyFile_ReportsRequestCompletelySentOff`; the existing chunked
  upload tests pin the 5-byte case.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch.
  It lists three complexity-12 members (`ExchangeAsync`, `HttpRequestBodyWriter.ReportStreamRead`,
  `HttpResponseHeadReader..ctor`) this task did not change; the build's `CA1502` gate passes.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. curl -v -d '' and -T of an empty file print Request completely sent off
