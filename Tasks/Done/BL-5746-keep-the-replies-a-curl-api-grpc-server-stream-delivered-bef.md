---
id: BL-5746
title: Keep the replies a Curl.Api gRPC server stream delivered before it ends in a non-OK status
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlClient.SendAsync.cs, Curl.Api/ResponseBodies/ResponseBodyStream.cs, Curl.Api/ResponseBodies/ResponseBodyBuffer.cs, Curl.Api.UnitTests/ResponseBodies/ResponseBodyStreamTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5746 — Keep the replies a Curl.Api gRPC server stream delivered before it ends in a non-OK status

## Goal

When a gRPC server stream sent through `Curl.Api` ends in a non-OK status (13, or a deadline), the replies received before the status are still available to the caller, as the console and curl-mcp keep them in stdout, and the error is reported after them.

## Context

From Surly cookbook bug report `2026-11-22T0726-Curl-bug-report.md` (batch 10 of the 1,000-recipe plan), finding 2. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl grpc://127.0.0.1:50051/` and a `Curl.Api` call to `surl.test.Echo/Expand` with `{"message":"hi","count":5,"failAfter":3}` (or with a deadline): `CurlException` 22 and no replies. `curl --grpc-proto echo.proto -d ... grpc://.../surl.test.Echo/Expand` prints the three replies, then exits 22. The console streams the body to stdout as it arrives and reports the failure at the end; `Curl.Api` appears to buffer and throw before handing the body over. Decide how `Curl.Api` exposes this (ADR, decided by Claude under Stewart's delegation): for example the response stream delivers the replies and the exception is raised when the stream reaches its end, or the `CurlException` carries the partial body. Keep it consistent with how `Curl.Api` treats any HTTP transfer that fails after part of the body arrived (exit 18, 56, 28).

## Acceptance criteria

- [x] An ADR records how `Curl.Api` hands over a body that ends in a failure, for gRPC and for HTTP alike.
- [x] Against a gRPC test server that sends three replies and then status 13, a `Curl.Api` caller reads the three replies and then gets the error; the same with a deadline. Pinned by tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix was in ResponseBodyStream.StartAsync (threw on failure even with body delivered), not SendAsync; widened touches, no other Doing task holds them. ADR-1334. Pinned at the stream level with a fake transfer (3 bytes then failure, then error on read), same path for gRPC status 13, deadline and HTTP 18/56/28. Quality run: 0 failing members.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 2.
- 2026-11-22: Backlog -> Doing. Criteria at claim: cbe82414 c6e3fcfe 3d836d5e.
- 2026-11-22: Doing -> Done. Curl.Api hands over the body a failed transfer delivered before its error (ADR-1334)
