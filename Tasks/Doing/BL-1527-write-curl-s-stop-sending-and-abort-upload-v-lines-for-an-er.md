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
completed:
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

- [ ] The measured stderr of a 500 and of a 301 under `-L` arriving while sending is in Notes.
- [ ] A test in `Curl.Protocol.Http.UnitTests` pins the `*` and `<` lines from the status line to the empty line for each measured case.
- [ ] `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
