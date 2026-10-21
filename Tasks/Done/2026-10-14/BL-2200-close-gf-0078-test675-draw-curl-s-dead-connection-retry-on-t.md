---
id: BL-2200
title: Close GF-0078 test675: draw curl's dead-connection retry on the progress meter (one draw, a newline, a new zero line)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2200 — Close GF-0078 test675: draw curl's dead-connection retry on the progress meter (one draw, a newline, a new zero line)

## Goal

Curl's standard error for upstream test675 matches curl 8.22.0's, so a later gap analysis measures `behaviour:test675` (gap finding GF-0078) as `match`.

## Context

- Split out of BL-2170, which closed GF-0078's other item (`behaviour:test1498`) in Curl.Console alone.
- test675: two URLs with different `user:password`, reply `200 OK swsclose`. Recorded under `C:/curl-crosscheck/recorded/675` (`reference/stderr.txt` against `curl/stderr.txt`).
- The reference's second transfer meter is: zero line, one more zero draw, CR LF, a new zero line, then the three `100 26 100 26` done lines. That is curl reusing the first connection, finding it dead before any response ("Connection died, retrying a fresh connect"), `Curl_pgrsDone` drawing once and writing its newline, and the retried transfer's meter starting again.
- Curl writes zero line then the done lines with no retry line: either its connection pool drops the closed connection before reuse (no retry at all), or the handler retries without the extra line. Find out which first, with `-v` on both (`Record-CurlExchange.ps1`, several runs, to separate a fixed rule from timing: the reference may only reuse because sws had not closed yet).
- If Curl does retry: `HttpProtocolHandler` reports the fresh-connection retry through `ReportRequestRetried` (line ~689, `ConnectAndExchangeAsync(reconnect, ...)`), which `TransferProgressRecorder` draws like an auth retry (two draws, newline, zero line, keeping the start time). curl draws a died-connection retry with one draw (`Curl_pgrsDone`), a newline and a zero line. That needs a distinct report on `ITransferProgress` (Abstractions) or a flag, drawn by `TransferProgressRecorder` in Curl.Console.

## Acceptance criteria

- [x] `behaviour:test675`: the Notes record measured evidence that the reference's extra line is a rule of curl 8.22.0, not timing, with the decision in an ADR (ADR-0489); the half that makes the item measure `match` (the pool's reuse rule) is split out as BL-2203, which carries that criterion.
- [x] A focused unit test in Curl.Console.UnitTests (and the HTTP handler's tests if it changes) pins the died-connection retry's meter lines (ADR-0482).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Found first (2026-10-13, `Record-CurlExchange.ps1 -Connections 2`, `200 OK` + `Content-Length: 26`, server closes after each response, `-v -u user1:password1 URL -u user2:password2 URL`): Curl does not retry at all. Its pool finds the closed connection dead (`Connection 0 seems to be dead`, 3 of 3 runs, ADR-0467). curl 8.22.0 (`Git\ucrt64\bin\curl.exe`, the gap office's reference) reuses it and takes `Connection died, retrying a fresh connect` 3 of 3, 3 of 3 more with `-HalfCloseAfterResponse -HoldOpenMilliseconds 500`, and with `--rate 120/m` (0.5 s gap); with `--rate 30/m` (2 s gap) it connects afresh with no dead or shutting-down line. curl 8.21.0 Schannel: `seems to be dead` 2 of 3, the 8.22.0 lines 1 of 3. So the extra line is a rule of 8.22.0 (no liveness check on a connection used under about a second ago), not timing.
- Split: the pool rule lives in `PoolingConnector` (Curl.Networking.UnitLibrary), which BL-2189 holds in Doing, so it is filed as BL-2203 (depends on this task) with the measurements and the test675 `match` criterion. This task did the meter half: ADR-0489, `ITransferProgress.ReportRequestResentOnFreshConnection` (default: `ReportRequestRetried`), reported by `HttpProtocolHandler` for the resend after `DiedBeforeResponse`, drawn by `TransferProgressRecorder` with one done draw, a newline and a new zero line (start time kept, as for an authentication retry).
- Touches widened (no Doing task names them, checked on origin/work/dark-factory): `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests`, because `MaxTimeWatchdog` and `LowSpeedWatchdog` wrap the sink and forward `ReportRequestRetried` explicitly; without a forwarder of their own, under `-m` or `-Y` the new report would reach the recorder as a retry.
- Tests: `TransferProgressRecorderTests.ReportRequestResentOnFreshConnection_*` (the test675 lines and the kept start time), `HttpProtocolHandlerTests.ExecuteAsync_ReusedConnectionResetBeforeTheResponse_ReportsTheResendOnAFreshConnection`, `HttpTransferProgressTests.ReportRequestResentOnFreshConnection_*`, `NoTransferProgressTests.ReportRequestResentOnFreshConnection_*` (the default body), and one each in `MaxTimeWatchdogTests` and `LowSpeedWatchdogTests`. Every new branch has a test; `Measure-CodeQuality.ps1` was not run (five small libraries touched, 30-45 minutes each under a shift); CI's quality job measures them.
- `--ai-help`: no option changed, so nothing to update.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. A died connection's resend is drawn as curl 8.22.0 draws it (one draw, a newline, a new zero line); the pool reuse rule that makes test675 reach it is BL-2203
