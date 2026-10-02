---
id: BL-909
title: Pin FR-064 url-query refusal, FR-042 mqtt user info and FR-085 send-error text with tests
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-909 — Pin FR-064 url-query refusal, FR-042 mqtt user info and FR-085 send-error text with tests

## Goal

Three requirement rows that BL-665 found built but untested, or with stale wording, each cite a test that drives the exact case they state.

## Context

- Found by BL-665 (2026-09-29) while checking `Documentation/Product/Requirements.md` against the tests.
- FR-064: no test drives `--url-query "a b=c"` to exit 3; the refusal only comes indirectly from `CurlUrl` parsing (`CurlCommandRunnerTests.RunAsync_UrlCurlRejects_PrintsCurlsReasonAndReturns3`).
- FR-042: `TransferCredentialLookup` applies URL user information for every scheme, but it is tested only with HTTP and FTP, never an `mqtt://` URL.
- FR-085: the row says the send-error message is "not pinned", yet `HttpProtocolHandlerTests.ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage` pins one; the row's wording is stale.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test runs `--url-query "a b=c"` and asserts exit 3 with curl's message; FR-064 cites it.
- [ ] A `Curl.Console.UnitTests` test runs an `mqtt://user:pass@host/topic` URL and asserts the user name and password reach the MQTT transfer; FR-042 cites it.
- [ ] FR-085 no longer says the message is not pinned and cites the HTTP test that pins it.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
