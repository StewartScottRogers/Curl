---
id: BL-2510
title: Curl.Api: CurlHttpMessageHandler maps HttpRequestMessage and HttpResponseMessage
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2509, BL-2507, BL-2506, BL-2505, BL-2503]
touches: [Curl.Api/HttpMessageHandler/Core, Curl.Api.UnitTests/HttpMessageHandler/Core, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2510 — Curl.Api: CurlHttpMessageHandler maps HttpRequestMessage and HttpResponseMessage

## Goal

`CurlHttpMessageHandler : HttpMessageHandler` lets `new HttpClient(new CurlHttpMessageHandler(client))` send a request and read a streamed response: method, URI, version, request and content headers, `HttpContent` bodies, status, reason, response headers and trailers, and cancellation through the `HttpClient` token.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Built on `CurlClient`. The response `HttpContent` wraps the response body stream; response headers split into message headers and content headers by name. `HttpClient.Timeout` surfaces as `TaskCanceledException` as `HttpClient` expects; a `CurlException` becomes `HttpRequestException` with the exception as inner.

## Acceptance criteria

- [x] Tests drive an `HttpClient` over the handler and a fake engine: GET, POST with string, stream and form content, headers on both sides, a streamed body read in pieces, HEAD, trailers, a `CurlException` as `HttpRequestException` and `HttpClient` timeout and cancellation.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Added `Curl.Api/CLAUDE.md` to `touches`: the last criterion keeps it true, and no task in Doing on
  `origin/work/dark-factory` names it.
- Design in ADR-0647 (decided by Claude under Stewart's delegation): the handler does not own the
  client (its `Dispose` is synchronous and blocking on `DisposeAsync` is forbidden); every `HttpContent`
  goes as a stream body with its `ContentLength` (so multipart is .NET's own bytes under its
  `Content-Type`), `Content-Length` is left to the engine; `Version`/`VersionPolicy` become
  `CURLOPT_HTTP_VERSION` on the client's `CurlHttpOptions`; response headers split by whether
  `HttpResponseHeaders` accepts them; `TrailerFillingStream` fills `TrailingHeaders` at the body's end.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` - 100% line and branch, 384 members, 0 failing,
  worst CRAP 10 (a first run caught `VersionOf` at complexity 12; HTTP/1 split out). Fast tests: all
  green, Curl.Api.UnitTests 818 passed. Ratchet file untouched.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: cdd679a2 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. HttpClient over CurlHttpMessageHandler sends requests through CurlClient and streams responses with headers and trailers
