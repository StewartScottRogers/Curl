---
id: BL-5960
title: Write curl's -v lines for a final response that arrives while the body waits for 100 Continue without a resend
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ExchangeSetup.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Original.Protocol.Http.UnitTests/Cgr067Tests.cs, Curl.Original.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.UploadReports.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ContinueWaitVerbose.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5960 — Write curl's -v lines for a final response that arrives while the body waits for 100 Continue without a resend

## Goal

A final response that arrives while the request body waits for `100 Continue`, and leads to no resend, produces the same `-v` lines, the same order of reading and sending, and the same end of connection as curl 8.22.0.

## Context

Measured by BL-5952 with `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500` for `curl -sS -v -d x=1 -H "Expect: 100-continue" --expect100-timeout 5 http://127.0.0.1:PORT/`, each response with a body matching its `Content-Length` (reference curl 8.22.0; its debug-only lines left out):

| Response | curl 8.22.0 after the head's last header line | Curl |
| --- | --- | --- |
| `500`, `Content-Length: 4` | `* HTTP error before end of send, stop sending`, `* abort upload`, `< `, body, `* shutting down connection #0` | `< `, body, `* Connection #0 to host ... left intact` |
| `417`, `Content-Length: 4`, `Connection: close` | `< `, body, `* we are done reading and this is set to close, stop send`, `* abort upload`, `* shutting down connection #0` | `< `, body, `* shutting down connection #0` |
| `200`, `Content-Length: 2` | head, body, `* Done waiting for 100-continue`, the 3 body bytes sent, `* upload completely sent off: 3 bytes`, `left intact` | `* Done waiting for 100-continue`, upload, then the head and body |

With `-f` and a `417`, both write no upload line. Start at `HttpProtocolHandler.ResponseExchange.cs`, `HttpProtocolHandler.UploadReports.cs` and `HttpContinueWaitConnection.cs`. `HttpConnectionInfoLines.AbortUpload(0)` already gives `abort upload`. Measure the other statuses (a `3xx` with and without `-L`, `401`) before pinning them; the `-L` case did not finish under the recorder within 45 seconds, so give each measurement a timeout.

## Acceptance criteria

- [x] For the `500` and the closing `417` exchanges above, Curl's `-v` lines from the status line to the connection's last line are curl 8.22.0's, and the connection is shut down where curl shuts it down, pinned by unit tests.
- [x] For the `200` exchange above, Curl writes the `-v` lines in curl 8.22.0's order, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 with `Record-CurlExchange.ps1 -HoldOpenMilliseconds 3000 -m 6`, `-d x=1 -H "Expect: 100-continue" --expect100-timeout 2`. A `500`, `401` or `301` (with or without `-L`; `--fail-with-body` too) on a connection that stays open: `HTTP error before end of send, stop sending`, `abort upload` before the empty line, then `shutting down connection #0`; no `Need to rewind upload`, no `Ignoring the response-body`. A `200`, `417` or `500` with `Connection: close`: response read at once, then `we are done reading and this is set to close, stop send`, `abort upload`, `shutting down`. A kept `200`: response, `Done waiting for 100-continue`, upload, `left intact`. A `500` with no length: response read while the wait runs on, body sent when it runs out.
- Design: over HTTP/1.x a final status during the wait now leaves the body held with the wait still running (`HttpContinueWaitConnection.HoldsBody`); it is sent when the wait runs out, during a read or after the response, so a server that waits for the body before finishing its response cannot deadlock. HTTP/2 and HTTP/3 keep their earlier behaviour (`IsMultiplexedStream`): unmeasured there, and their stream connections are not shown safe for a write beside a pending read. BL-5961 filed for the status they assume.
- After the real binary was rebuilt, its lines for the `500`, closing `417` and kept `200` matched curl 8.22.0's.
- touches widened to RequestBodySending.cs, ExchangeSetup.cs, HttpRequestBodyWriter.cs, Cgr067Tests.cs (its BL-3596 wait test now pins the held body) and HttpContinueWaitConnectionTests.cs; no other task in Doing on origin/work/dark-factory named them.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary` found two methods over the complexity gate and one uncovered catch; all three fixed, checked with a Cobertura run of the Http unit tests (not a second full measurement).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 9cea44d2 71b88799 3d836d5e.
- 2026-11-25: Doing -> Done. Curl writes curl 8.22.0's -v lines, order and connection end for a final response that arrives while the body waits for 100 Continue without a resend
