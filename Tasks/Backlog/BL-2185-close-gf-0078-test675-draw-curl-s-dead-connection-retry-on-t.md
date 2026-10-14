---
id: BL-2185
title: Close GF-0078 test675: draw curl's dead-connection retry on the progress meter (one draw, a newline, a new zero line)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2185 — Close GF-0078 test675: draw curl's dead-connection retry on the progress meter (one draw, a newline, a new zero line)

## Goal

Curl's standard error for upstream test675 matches curl 8.22.0's, so a later gap analysis measures `behaviour:test675` (gap finding GF-0078) as `match`.

## Context

- Split out of BL-2170, which closed GF-0078's other item (`behaviour:test1498`) in Curl.Console alone.
- test675: two URLs with different `user:password`, reply `200 OK swsclose`. Recorded under `C:/curl-crosscheck/recorded/675` (`reference/stderr.txt` against `curl/stderr.txt`).
- The reference's second transfer meter is: zero line, one more zero draw, CR LF, a new zero line, then the three `100 26 100 26` done lines. That is curl reusing the first connection, finding it dead before any response ("Connection died, retrying a fresh connect"), `Curl_pgrsDone` drawing once and writing its newline, and the retried transfer's meter starting again.
- Curl writes zero line then the done lines with no retry line: either its connection pool drops the closed connection before reuse (no retry at all), or the handler retries without the extra line. Find out which first, with `-v` on both (`Record-CurlExchange.ps1`, several runs, to separate a fixed rule from timing: the reference may only reuse because sws had not closed yet).
- If Curl does retry: `HttpProtocolHandler` reports the fresh-connection retry through `ReportRequestRetried` (line ~689, `ConnectAndExchangeAsync(reconnect, ...)`), which `TransferProgressRecorder` draws like an auth retry (two draws, newline, zero line, keeping the start time). curl draws a died-connection retry with one draw (`Curl_pgrsDone`), a newline and a zero line. That needs a distinct report on `ITransferProgress` (Abstractions) or a flag, drawn by `TransferProgressRecorder` in Curl.Console.

## Acceptance criteria

- [ ] `behaviour:test675`: Curl's stderr matches curl 8.22.0's (`reference curl exits 0; stdout 136 bytes`), so the item measures `match`, or the Notes record measured evidence that the reference's extra line is timing, not a rule, with the decision in an ADR.
- [ ] A focused unit test in Curl.Console.UnitTests (and the HTTP handler's tests if it changes) pins the died-connection retry's meter lines (ADR-0482).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-13: Created.
