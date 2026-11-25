---
id: BL-5941
title: Write curl's -v lines for the response the redirect limit refuses to answer: Ignoring the response-body and left intact before the limit's message
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RedirectLimitVerbose.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5941 — Write curl's -v lines for the response the redirect limit refuses to answer: Ignoring the response-body and left intact before the limit's message

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

- [ ] For that exchange, the port's `-v` lines from the last status line to the end are curl 8.22.0's, in order.
- [ ] A unit test in `Curl.Original.Protocol.Http.UnitTests` pins those lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
