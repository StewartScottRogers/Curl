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
completed:
---
# BL-1446 — Match curl's -v lines after a 417 that arrives while the body is being sent

## Goal

After a 417 that arrives while the body is being sent, Curl's `-v` lines between `* Got HTTP failure 417 while sending data` and the resend match curl 8.21.0's.

## Context

- BL-319 Notes measured curl 8.21.0 writing, for `--data-binary @big.bin` answered 417 after 64 KiB: `* Done waiting for 100-continue`, `* Got HTTP failure 417 while sending data`, `* Need to rewind upload for next request`, `* abort upload after having sent 65536 bytes`, `* shutting down connection #0`, `* Issue another request to this URL: ...`.
- BL-1430 added the `Got HTTP failure 417` line (`HttpProtocolHandler.RetryOfAsync`). Curl today then writes `* Ignoring the response-body`, `* setting size while ignoring`, `< `, `* shutting down connection #0` and no `Need to rewind upload` / `abort upload` lines (`HttpProtocolHandlerTests.ProxyConnectionAnd417Lines.cs`, `ExecuteAsync_417WhileSendingTheBody_ReportsGotFailureWhileSendingBeforeTheResend`).
- Measure the whole stderr first with `Record-CurlExchange.ps1 -RespondAfterBodyBytes 65536` (a `-RespondAfterBodyBytes 0` run hung for 30 minutes under BL-1430: use a body larger than the socket buffers, and a timeout).

## Acceptance criteria

- [ ] The measured stderr of the 417-while-sending case is in Notes.
- [ ] A test in `Curl.Protocol.Http.UnitTests` pins every `*` and `<` line from the 417's status line to the resend's `>` line in the measured order.
- [ ] `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
