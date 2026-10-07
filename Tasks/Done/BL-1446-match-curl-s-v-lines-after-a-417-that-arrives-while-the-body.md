---
id: BL-1446
title: Match curl's -v lines after a 417 that arrives while the body is being sent
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-090
created: 2026-10-04
completed: 2026-10-07
---
# BL-1446 — Match curl's -v lines after a 417 that arrives while the body is being sent

## Goal

After a 417 that arrives while the body is being sent, Curl's `-v` lines between `* Got HTTP failure 417 while sending data` and the resend match curl 8.21.0's.

## Context

- BL-319 Notes measured curl 8.21.0 writing, for `--data-binary @big.bin` answered 417 after 64 KiB: `* Done waiting for 100-continue`, `* Got HTTP failure 417 while sending data`, `* Need to rewind upload for next request`, `* abort upload after having sent 65536 bytes`, `* shutting down connection #0`, `* Issue another request to this URL: ...`.
- BL-1430 added the `Got HTTP failure 417` line (`HttpProtocolHandler.RetryOfAsync`). Curl today then writes `* Ignoring the response-body`, `* setting size while ignoring`, `< `, `* shutting down connection #0` and no `Need to rewind upload` / `abort upload` lines (`HttpProtocolHandlerTests.ProxyConnectionAnd417Lines.cs`, `ExecuteAsync_417WhileSendingTheBody_ReportsGotFailureWhileSendingBeforeTheResend`).
- Measure the whole stderr first with `Record-CurlExchange.ps1 -RespondAfterBodyBytes 65536` (a `-RespondAfterBodyBytes 0` run hung for 30 minutes under BL-1430: use a body larger than the socket buffers, and a timeout).

## Acceptance criteria

- [x] The measured stderr of the 417-while-sending case is in Notes.
- [x] A test in `Curl.Protocol.Http.UnitTests` pins every `*` and `<` line from the 417's status line to the resend's `>` line in the measured order.
- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

- Measured 2026-10-07, curl 8.21.0 (Schannel, mingw64): `Record-CurlExchange.ps1 -Port 18446 -Connections 2 -RespondAfterBodyBytes 65536 -Response "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\n\r\n",... -CurlArgs -v,-s,--data-binary,@big.bin,http://127.0.0.1:18446/up` with a 2 MiB body (a 1 MiB body sends no `Expect`: curl adds it only above 1 MiB). Exit 0 after 1.1 s. stderr from the request head:
  ```
  > Expect: 100-continue
  >
  * Done waiting for 100-continue
  } [65536 bytes data]
  < HTTP/1.1 417 Expectation Failed
  < Content-Length: 0
  * Got HTTP failure 417 while sending data
  * Need to rewind upload for next request
  * abort upload after having sent 524288 bytes
  <
  * shutting down connection #0
  * Issue another request to this URL: 'http://127.0.0.1:18446/up'
  * Hostname 127.0.0.1 was found in DNS cache
  *   Trying 127.0.0.1:18446...
  * Established connection to 127.0.0.1 (127.0.0.1 port 18446) from 127.0.0.1 port 55917
  * using HTTP/1.x
  > POST /up HTTP/1.1
  ```
  The byte count is however much curl had handed the socket (timing-dependent); Curl writes `HttpRequestBodyWriter.BytesSent`.
- Changes in `HttpProtocolHandler`: `RetryOfAsync` writes `Need to rewind upload for next request` and the new `HttpConnectionInfoLines.AbortUpload(n)` after `Got HTTP failure 417 while sending data`; `ReportIgnoredBody` writes nothing once the upload was cut short (the connection is shut down, so curl never ignores a body on it); `ReportConnectionEnd` writes `Issue another request to this URL` after any retry on a new connection, not only after a connection that died - no other test pinned its absence. The DNS-cache, Trying and Established lines are the connector's (Curl.Networking), which the test's fake connector does not write; the test pins the rest, `using HTTP/1.x` included.
- The same run's second connection, answered 417 with no `Expect` on the resend, showed curl's `HTTP error before end of send, stop sending` / `abort upload after having sent N bytes` for a plain error while sending, which Curl does not write: filed as BL-1526.
- `dotnet test --filter "TestCategory!=Integration"`: every project green (Http 1839 passed). Measure-CodeQuality -Library Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. After a 417 while sending, -v writes curl's Need to rewind / abort upload lines, no Ignoring lines, and Issue another request before the resend
