---
id: BL-885
title: Rewind a seekable -T upload and resend it when an HTTP/3 stream is refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-834]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-885 — Rewind a seekable -T upload and resend it when an HTTP/3 stream is refused

## Goal

A `-T` upload whose body is a seekable file (`StreamBody` over a stream with `CanSeek`) is rewound and sent again on a new connection when the server refuses its HTTP/3 stream with `H3_REQUEST_REJECTED`, as curl 8.21.0's `Curl_retry_request` does with `Curl_creader_set_rewind` (ADR-0187 decision 2); an unseekable one (stdin) fails as curl does when the rewind is impossible.

## Context

- BL-834 added the refused-stream retry in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` (`StreamRefusedOutcome`), but, like the handler's other retries (`CanSendAgainOnFreshConnection`, `MayRetry`), it never sends a `StreamBody` again: a refused `-T` upload fails at once with exit 56 `Failure when receiving data from the peer`.
- curl rewinds the upload through its client reader; for a reader that cannot rewind, `Curl_creader_resume_from`/rewind fails with `CURLE_SEND_FAIL_REWIND` (65) `necessary data rewind wasn't possible`. Read `lib/sendf.c` at `curl-8_21_0` for the exact text before pinning it.
- The stream's start position is where the transfer began reading it (a `-C` offset may have moved it).

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` has a test where a refused `-T` upload from a seekable stream is sent again, whole, on a second fake QUIC connection and ends with exit 0.
- [x] A test pins what an unseekable upload does when refused, with the exit code and message read from `curl-8_21_0`'s source.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

- Read from curl-8_21_0's source (ADR-0279): `Curl_retry_request` sets the reader to rewind;
  `Curl_init_do` -> `Curl_client_start` rewinds before the new connection is connected;
  `cr_in_rewind` calls the tool's seek callback, which answers `CURL_SEEKFUNC_CANTSEEK` (2) on
  stdin, so curl fails with exit 65 `seek callback returned error 2` and the `-v` line
  `rewind of client reader 'cr-in' failed: 65`. Not measurable: no server refuses an HTTP/3
  stream on demand.
- `StreamRefusedOutcome` now rewinds a seekable `StreamBody` through the existing
  `HttpRequestBodyWriter.Rewound` (back to where the writer began reading, so a `-C` offset
  holds); an unseekable one sets `HttpRequestPlan.UploadCannotRewind`, and
  `FailureBeforeConnecting` fails the retry before `ConnectAsync`.
- Default taken: curl skips the rewind when its read callback was never called; on HTTP/3 the
  body is always written before the response is read, so that case is not modelled.
- Default taken: ADR number 0278, not 0276/0277, because other lanes already hold 0276 and
  lane 3 may take 0277.
- Added `Documentation/Planning/Decisions` to `touches` for the ADR; no task in Doing names it.
- The quality gate failed on two members that predate this task (`HttpNtlmInfoLines.OffersNtlm`
  87.5% branch, `ProxyUrlOf` 50% branch); closed with `HttpNtlmInfoLinesTests` and
  `ExecuteAsync_ProxyCredential_AsksTheAuthenticatorAboutTheProxysOwnUrl`.
- `Measure-CodeQuality.ps1` over the whole solution hung for an hour in
  `Curl.Quic.UnitTests`' testhost under the coverage collector; measured instead from the
  coverage of `Curl.Protocol.Http.UnitTests`, `Curl.Console.UnitTests` and
  `Curl.Protocol.Abstractions.UnitTests` with `-SkipTestRun`: 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. A -T upload whose HTTP/3 stream is refused is rewound and sent again; stdin fails with exit 65 as curl does
