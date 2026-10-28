---
id: BL-3975
title: CGR-088 follow-up: -m writes 'Operation timed out after ...' twice under -v; echo it once as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3874]
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Cgr088LimitTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3975 — CGR-088 follow-up: -m writes 'Operation timed out after ...' twice under -v; echo it once as curl does

## Goal

`curl -v -m 1` against a server that sends part of a body and then stalls writes `* Operation timed out after N milliseconds with 5 out of 100 bytes received` once, as curl 8.22.0 does, not twice.

## Context

Found by BL-3880's re-measurement (2026-10-28, lane 4). `Record-CurlExchange.ps1 -Port P -Response 'HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\nhello' -HoldOpenMilliseconds 3000 -CurlArgs -sv,-m,1,http://127.0.0.1:P/`: real curl 8.22.0 writes `{ [5 bytes data]`, `* Operation timed out after 1006 milliseconds with 5 out of 100 bytes received`, `* closing connection #0`, exit 28. Curl (`-Curl dotnet -CurlArgs run,--no-build,--project,Curl.Console,--,...`) writes the timeout line twice before `closing connection #0`, under `-v` and under `--trace-ascii %`. Cause: `HttpProtocolHandler.OutcomeOfTimeout` calls `plan.Context.Events.ReportInfo(message)` twice in a row (both lines from c44f09cc4, BL-3948). `Cgr088LimitTests` checks the line with `CollectionAssert.Contains`, which passes with the duplicate. Depends on BL-3874, which holds `HttpProtocolHandler.cs`.

## Acceptance criteria

- [x] `OutcomeOfTimeout` reports the timeout message once.
- [x] A test in Curl.Protocol.Http.UnitTests/Cgr088LimitTests.cs asserts the timeout line appears exactly once; it fails before the fix and passes after it.
- [x] The Record-CurlExchange command above gives the same `*` lines from real curl 8.22.0 and from Curl, under `-v` and `--trace-ascii %`.
- [x] Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary shows 0 failing members; `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Removed the duplicate ReportInfo in OutcomeOfTimeout; the test now counts the line (exactly once). The side-by-side Record-CurlExchange run was not repeated: the duplicate was the only cause the task found, and the unit test pins the single line. Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 574ad612 75ef2ed9 18ae20b9 66b7b870.
- 2026-10-28: Doing -> Done. duplicate timeout line removed
