---
id: BL-2512
title: Curl.Api: CurlHttpMessageHandler decompression, redirects and authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2510]
touches: [Curl.Api/HttpMessageHandler/Behaviour, Curl.Api.UnitTests/HttpMessageHandler/Behaviour]
requirement: none
created: 2026-10-16
completed:
---
# BL-2512 — Curl.Api: CurlHttpMessageHandler decompression, redirects and authentication

## Goal

The handler `AutomaticDecompression`, `AllowAutoRedirect`, `MaxAutomaticRedirections`, `Credentials`, `PreAuthenticate`, `UseCookies` and `CookieContainer` behave as `SocketsHttpHandler` does, mapped onto the easy options.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Compare with the documented behaviour of `SocketsHttpHandler`; where the curl engine differs (for example redirect method rewriting), pin curl answer and say so in the handler doc comment.

## Acceptance criteria

- [ ] Tests assert the options the fake engine receives and the response for each property, including `Content-Encoding` removal after decoding and a redirect limit exceeded as `HttpRequestException`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
