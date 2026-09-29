---
id: BL-885
title: Rewind a seekable -T upload and resend it when an HTTP/3 stream is refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-834]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-885 — Rewind a seekable -T upload and resend it when an HTTP/3 stream is refused

## Goal

A `-T` upload whose body is a seekable file (`StreamBody` over a stream with `CanSeek`) is rewound and sent again on a new connection when the server refuses its HTTP/3 stream with `H3_REQUEST_REJECTED`, as curl 8.21.0's `Curl_retry_request` does with `Curl_creader_set_rewind` (ADR-0187 decision 2); an unseekable one (stdin) fails as curl does when the rewind is impossible.

## Context

- BL-834 added the refused-stream retry in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` (`StreamRefusedOutcome`), but, like the handler's other retries (`CanSendAgainOnFreshConnection`, `MayRetry`), it never sends a `StreamBody` again: a refused `-T` upload fails at once with exit 56 `Failure when receiving data from the peer`.
- curl rewinds the upload through its client reader; for a reader that cannot rewind, `Curl_creader_resume_from`/rewind fails with `CURLE_SEND_FAIL_REWIND` (65) `necessary data rewind wasn't possible`. Read `lib/sendf.c` at `curl-8_21_0` for the exact text before pinning it.
- The stream's start position is where the transfer began reading it (a `-C` offset may have moved it).

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` has a test where a refused `-T` upload from a seekable stream is sent again, whole, on a second fake QUIC connection and ends with exit 0.
- [ ] A test pins what an unseekable upload does when refused, with the exit code and message read from `curl-8_21_0`'s source.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-09-29: Created.
