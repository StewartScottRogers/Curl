---
id: BL-2512
title: Curl.Api: CurlHttpMessageHandler decompression, redirects and authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2510]
touches: [Curl.Api/HttpMessageHandler/Behaviour, Curl.Api.UnitTests/HttpMessageHandler/Behaviour, Curl.Api/HttpMessageHandler/Core/CurlHttpMessageHandler.cs, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2512 — Curl.Api: CurlHttpMessageHandler decompression, redirects and authentication

## Goal

The handler `AutomaticDecompression`, `AllowAutoRedirect`, `MaxAutomaticRedirections`, `Credentials`, `PreAuthenticate`, `UseCookies` and `CookieContainer` behave as `SocketsHttpHandler` does, mapped onto the easy options.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Compare with the documented behaviour of `SocketsHttpHandler`; where the curl engine differs (for example redirect method rewriting), pin curl answer and say so in the handler doc comment.

## Acceptance criteria

- [x] Tests assert the options the fake engine receives and the response for each property, including `Content-Encoding` removal after decoding and a redirect limit exceeded as `HttpRequestException`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Touches widened to `Curl.Api/HttpMessageHandler/Core/CurlHttpMessageHandler.cs` (the handler had to become `partial` and its `SendAsync` call the behaviour hooks; no Doing task on `origin/work/dark-factory` named it) and `Curl.Api/CLAUDE.md` (an acceptance criterion names it).
- Decisions in ADR-0649 (Decided by Claude under Stewart's delegation): `SocketsHttpHandler`'s defaults (redirects on, 50; cookies on) win over the client's options for what they map; `CURLFOLLOW_OBEYCODE` because it rewrites methods after 303 as .NET does; a redirect limit exceeded throws `HttpRequestException` (curl error 47) where .NET returns the last 3xx; the effective URL becomes `RequestUri`; decompression lists `gzip, deflate, br` and removes the whole `Content-Encoding` and `Content-Length` since the engine decodes every coding it knows; credentials take the strongest scheme's credential (Negotiate, NTLM, Digest, Basic) with `CURLAUTH_ONLY` unless `PreAuthenticate`; cookies go as `CURLOPT_COOKIE`, the final response's `Set-Cookie` stored, a redirect's own ones not (they never reach the handler).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` first flagged `CredentialFor` at complexity 20; split into a LINQ query and `UsableCredential`, then 0 failing members, 100% line and branch, worst CRAP 10. Fast tests all green (Curl.Api.UnitTests 840 passed, 2 skipped); `PassingUpstreamCases.txt` unchanged and Curl.Conformance.UnitTests green.
- `dotnet format --verify-no-changes` on Curl.Api.UnitTests reports an imports-order issue only in `EngineContracts/Multi/MultiValueTypeTests.cs`, a file this task did not touch; left alone.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 8f852fae 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlHttpMessageHandler honours AutomaticDecompression, AllowAutoRedirect, MaxAutomaticRedirections, Credentials, PreAuthenticate, UseCookies and CookieContainer with SocketsHttpHandler's defaults
