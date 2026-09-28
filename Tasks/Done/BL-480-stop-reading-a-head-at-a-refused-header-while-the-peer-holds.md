---
id: BL-480
title: Stop reading a head at a refused header while the peer holds it open
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-479]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-480 — Stop reading a head at a refused header while the peer holds it open

## Goal

When a final response head carries a header curl 8.21.0 refuses and the peer then sends nothing more without closing, `HttpProtocolHandler` fails at once with the refused header's exit code and message, as curl 8.21.0 does, instead of waiting for the head to end or for `-m` to expire.

## Context

- Since BL-475 and BL-479, `HttpResponseHeadReader` asks `FindRefusal` of a final head's whole headers only when the head ends (empty line or peer close) or fails. A peer that holds the connection open after a refused header leaves the reader waiting in `HttpLineReader.ReadLineAsync`.
- Checking each header as it arrives was rejected in BL-475 as O(n²) (BL-475 Notes). A route that keeps the cost bounded: ask `FindRefusal` of the whole headers held only when the line reader has no whole line buffered and must read from the connection.
- Measure first: `Record-CurlExchange.ps1` sends the response and closes, so extend it (for example a `-HoldOpenMilliseconds` parameter) to keep the connection open after the response, then run `-s -v http://127.0.0.1:<port>/` against `HTTP/1.1 200 OK`, `X-Before: 1`, `Content-Length: x` with no empty line, and time how long curl takes to exit.

## Acceptance criteria

- [x] A test over a scripted connection that never ends the head after `Content-Length: x` pins exit 8, `Invalid Content-Length: value` and the `-v` head lines before it, as measured, without the connection closing.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-479 (2026-09-27).
- `Record-CurlExchange.ps1` gained `-HoldOpenMilliseconds`: after each response the server keeps the connection open until curl closes it or that long passes without a byte, recording what curl sends. The script now also reports how long curl ran.
- Measured (2026-09-27, curl 8.21.0, `-s -v http://127.0.0.1:<port>/`, `-HoldOpenMilliseconds 10000`/`20000`) against `HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\n` followed by:
  - `X-After: 1\r\n`, or just `X`, then held: exit 8 within 60 ms; `< HTTP/1.1 200 OK`, `< X-Before: 1`, `* Invalid Content-Length: value`, `* closing connection #0`.
  - nothing, then held: curl waits for the peer. Exit 0 when the server finally closed (after 20 s), with `< Content-Length: x` reported and `* Connection #0 ... left intact`. With `-m 3`: exit 28, `Operation timed out after 3010 milliseconds with 0 bytes received`.
  - a single blank (` `), then held: waits the same way (exit 0 at close), since a blank folds the next line into the header.
  - nothing, then an immediate close (no hold): exit 0, `< Content-Length: x`, `left intact`.
- So the Goal's premise was half right: curl 8.21.0 refuses a header only once a byte of the next line shows it whole, not as soon as its own line ends. Implemented to match the measurement, not the Goal's wording; the acceptance test sends a byte of the next line. No ADR: the behaviour is measured, not chosen.
- Route taken: `HttpLineReader.EndsBeforeRead`, asked before each read from the connection with the unfinished line's bytes. `HttpResponseHeadReader.EndsAtRefusedHeader` answers it for a final head: the held whole headers, plus the pending header when the unfinished line has begun with a non-blank byte (`HttpResponseHeadBuilder.PendingHeader`), go to `FindRefusal`; a refusal ends the lines as if the peer had closed, and `ReadAsync` finds the same refusal in the head it builds, so the existing release/drop path runs unchanged.
- Cost: `FindRefusal` is asked only before a read that would wait on the network, and only when more headers are whole than last asked. A head that arrives before the reader waits is never asked. The worst case is a peer that sends each header in its own segment: one O(n) check per header. Accepted as the route the task's Context proposed; `-m` bounds it.
- Tests: `ExecuteAsync_PeerHoldsTheHeadOpenAfterTheRefusedHeader_FailsWithoutWaiting` (whole line / one byte after, every chunk size; both timed out at 30 s with the hook disabled) and `ExecuteAsync_PeerHoldsTheHeadOpenRightAfterTheRefusedHeader_WaitsForMaxTime` (nothing / a blank after: exit 28 at `-m`).
- Follow-up filed: BL-482. curl exits 0 when the peer closes right after a refused header's line; we still fail with exit 8 there, since the head built at close includes that last header.
- `dotnet format --verify-no-changes` flags end-of-line markers in `Curl.Output.UnitTests\TraceTransferEventWriterOpenSslTlsTests.cs`, a file this task did not touch; both HTTP projects verify clean.
- Delivered in-session rather than through the full `/feature` agent chain: one hook in two classes, with the route laid out in Context.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A refused response header fails the transfer at once when a byte of the next line arrives while the peer holds the head open, as curl 8.21.0 does
