---
id: BL-5960
title: Write curl's -v lines for a final response that arrives while the body waits for 100 Continue without a resend
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.UploadReports.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ContinueWaitVerbose.cs]
requirement: none
created: 2026-11-25
completed:
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

- [ ] For the `500` and the closing `417` exchanges above, Curl's `-v` lines from the status line to the connection's last line are curl 8.22.0's, and the connection is shut down where curl shuts it down, pinned by unit tests.
- [ ] For the `200` exchange above, Curl writes the `-v` lines in curl 8.22.0's order, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 9cea44d2 71b88799 3d836d5e.
