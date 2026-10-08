---
id: BL-1527
title: Write curl's stop-sending and abort-upload -v lines for an error status that arrives while the body is sent
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-090
created: 2026-10-07
completed: 2026-10-07
---
# BL-1527 — Write curl's stop-sending and abort-upload -v lines for an error status that arrives while the body is sent

## Goal

When a status of 300 or above that leads to no resend arrives while the request body is being sent, Curl's `-v` writes curl 8.21.0's `* HTTP error before end of send, stop sending` and `* abort upload after having sent N bytes` after the response's headers and before its empty line.

## Context

- Found under BL-1446. curl 8.21.0 (Schannel), `-v -s --data-binary @2MiB.bin` without `Expect` (so the 417 is a plain error), answered after 65536 body bytes by `HTTP/1.1 417 Expectation Failed` / `Content-Length: 0`, wrote:
  `< HTTP/1.1 417 Expectation Failed`, `< Content-Length: 0`, `* HTTP error before end of send, stop sending`, `* abort upload after having sent 655204 bytes`, `< `, then `* shutting down connection #1`.
- Curl writes neither line today: `grep "abort upload\|before end of send"` finds only a doc comment in `HttpContinueWaitConnection.cs`. BL-1446 added `HttpConnectionInfoLines.AbortUpload` for the 417-resend case; reuse it.
- `HttpProtocolHandlerTests.ErrorWhileSending.cs` drives a 500 and a 301 arriving while sending (`FailingWhileSending`). Measure a 500 and a 3xx with `-L` too (`Record-CurlExchange.ps1 -RespondAfterBodyBytes 65536`, body over 1 MiB, with a timeout) before pinning.

## Acceptance criteria

- [x] The measured stderr of a 500 and of a 301 under `-L` arriving while sending is in Notes.
- [x] A test in `Curl.Protocol.Http.UnitTests` pins the `*` and `<` lines from the status line to the empty line for each measured case.
- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

Measured 2026-10-07, curl 8.21.0 (Schannel), `Record-CurlExchange.ps1 -RespondAfterBodyBytes 65536`,
`-v -s -m 20 -H Expect: --data-binary @big.bin` (2097152 bytes); request.bin 655360 bytes each time.

500 (`Content-Length: 4`, body `fail`), exit 0:
```
} [65381 bytes data]
< HTTP/1.1 500 Internal Server Error
< Content-Length: 4
* HTTP error before end of send, stop sending
* abort upload after having sent 655205 bytes
<
{ [4 bytes data]
* shutting down connection #0
```
301 without `-L`: the same two lines after `< Location: /v` and `< Content-Length: 0`, then `< `
and `* shutting down connection #0`.

301 with `-L` (a second connection answered 200), exit 0 - no stop-sending or abort-upload line:
```
< HTTP/1.1 301 Moved Permanently
* Need to rewind upload for next request
* close instead of sending 1441947 more bytes
< Location: /v
< Content-Length: 0
* Keep sending data to get tossed away
<
* shutting down connection #0
* Issue another request to this URL: 'http://127.0.0.1:18528/v'
```
With `-H "Transfer-Encoding: chunked"` the second line is `* close instead of sending unknown amount of more bytes`.
curl still stops sending in this case (request.bin 655440 bytes), as Curl already did.

Done: `HttpProtocolHandler.ReportUploadStopped` writes the stop-sending and abort-upload lines (or,
for a followed redirect, `Keep sending data to get tossed away`) before the held empty line when
the body was cut short and no resend answers the response; the 417 resend keeps its own lines.
`ReportUploadRewind` adds `close instead of sending ...` after `Need to rewind upload` when the
body was cut short. Tests: `ExecuteAsync_ErrorWhileSendingTheBody_WritesStopSendingAndAbortUploadBeforeTheEmptyLine`
(500, 301 without `-L`), `ExecuteAsync_301FollowedWhileSendingTheBody_WritesCloseInsteadOfSendingAndKeepSending`,
and `HttpConnectionInfoLinesTests.CloseInsteadOfSending_WritesCurlsLine` (known and unknown length).
Measure-CodeQuality: Curl.Protocol.Http.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. -v writes curl's stop-sending and abort-upload lines for an error while sending, and close-instead/keep-sending for a followed redirect
