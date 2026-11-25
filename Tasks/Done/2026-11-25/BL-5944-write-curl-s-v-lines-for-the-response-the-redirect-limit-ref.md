---
id: BL-5944
title: Write curl's -v lines for the response the redirect limit refuses to answer: Ignoring the response-body and left intact before the limit's message
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RedirectLimitVerbose.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5944 — Write curl's -v lines for the response the redirect limit refuses to answer: Ignoring the response-body and left intact before the limit's message

## Goal

When an authentication retry is refused by the redirect limit, `-v` ends with the same `*` and
`<` lines, in the same order, as curl 8.22.0.

## Context

Found by BL-5932. For its `--proxy-ntlm --ntlm` exchange (the cycle is written out in that
task; `Record-CurlExchange.ps1 -Connections 1 -HoldOpenMilliseconds 2000
-AnswerHeldRequests 70`), curl 8.22.0 Schannel ends the last response with `< Content-Length: 0`,
`* Ignoring the response-body`, `* setting size while ignoring`, `< ` (empty),
`* Connection #0 to host 127.0.0.1:33013 left intact`, `* Maximum (50) redirects followed`.
The port writes `< Content-Length: 0`, `* Maximum (50) redirects followed`, `< `,
`* closing connection #0`. See `ThrowIfRedirectLimitReached` in `HttpProtocolHandler.cs`.
Measure whether a Digest or `--anyauth` retry at `--max-redirs 0` ends the same way.

## Acceptance criteria

- [x] For that exchange, the port's `-v` lines from the last status line to the end are curl 8.22.0's, in order.
- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests` pins those lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- curl refuses the resend only after the response is done (`multi.c` follows once the transfer ends). So `RetryOfAsync` now gives the retry and `RefusingResendPastRedirectLimit` turns the finished exchange into exit 47: the body is discarded as before any resend, the connection ends as a successful one's (`left intact`, or `shutting down` after `Connection: close`), then `Maximum (N) redirects followed`. No ADR: it only matches measured curl.
- Measured 2026-11-25 with `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500`, curl 8.22.0, `--max-redirs 0`: Digest, `--anyauth`, chunked, `Connection: close`, `-d`, `-f` and a 417 all end this way. Curl's `-v` lines from the status line on and its stdout are now identical, except for the two gaps filed below.
- The task's NTLM exchange (limit 50): Curl's whole stderr, 1015 lines, is identical to curl's once the connection's source port, the Authorization values and curl's debug-build lines are masked.
- A side effect that matches curl: the refused response's body now counts in `%{size_download}` (4 for a 4-byte body) and a chunked body's trailers reach `-i`/`-D`.
- Filed BL-5951 (`%{redirect_url}` of the refused resend) and BL-5952 (`abort upload` after a 417 while waiting); both predate this task.
- Gates: build clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary` 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: e09fbb4d 164d3fb1 3d836d5e.
- 2026-11-25: Doing -> Done. -v finishes the response the redirect limit refuses to resend (Ignoring the response-body, left intact) before Maximum (N) redirects followed, as curl does
