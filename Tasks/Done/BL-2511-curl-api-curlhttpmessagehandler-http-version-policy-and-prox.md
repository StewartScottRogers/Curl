---
id: BL-2511
title: Curl.Api: CurlHttpMessageHandler HTTP version policy and proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2510]
touches: [Curl.Api/HttpMessageHandler/Versions, Curl.Api.UnitTests/HttpMessageHandler/Versions, Curl.Api/HttpMessageHandler/Core, Curl.Api/HttpMessageHandler/Proxy, Curl.Api.UnitTests/HttpMessageHandler/Proxy, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2511 — Curl.Api: CurlHttpMessageHandler HTTP version policy and proxy

## Goal

The handler honours `HttpRequestMessage.Version` and `VersionPolicy` (`RequestVersionOrLower`, `RequestVersionOrHigher`, `RequestVersionExact`) for HTTP/1.1, HTTP/2 and HTTP/3, and a `Proxy` and credentials set on the handler reach the engine.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `EasyHttpOptions.HttpVersion` and `EasyProxyOptions`. Where the policy cannot be met the handler throws `HttpRequestException` as `SocketsHttpHandler` does.

## Acceptance criteria

- [x] Tests assert the option the fake engine receives for each version and policy, the failure when the policy cannot be met and the proxy settings (address, credentials, bypass list).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Touches widened (no task in Doing on origin/work/dark-factory names Curl.Api): the guard and proxy mapping are called from `CurlHttpMessageHandler` and `HttpRequestMapping` in `HttpMessageHandler/Core`; the handler's properties live in a partial half in the new `HttpMessageHandler/Proxy`; `Curl.Api/CLAUDE.md` is named by a criterion.
- Decisions (ADR-0648): only 1.0, 1.1, 2.0 and 3.0 are spoken (a higher version under OrLower is asked for as 3.0, the rest throw before a handle is made); the response's major version is checked after the transfer (Exact equal, OrHigher no lower, OrLower anything, since libcurl never goes above the version asked for), failing with `HttpRequestException` `VersionNegotiationError` after disposing the body. Proxy uses `HttpClientHandler`'s `Proxy`, `UseProxy`, `DefaultProxyCredentials` and `Credentials`, asked per request with `IsBypassed` and `GetProxy`; a direct connection is `CURLOPT_PROXY ""`. BypassList regular expressions are not translated to NOPROXY, since `IsBypassed` answers them.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 0 failing members; Curl.Api.UnitTests 858 passed, 2 skipped; the full fast run green; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: e5b4693b 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlHttpMessageHandler enforces HttpRequestMessage.VersionPolicy (VersionNegotiationError as SocketsHttpHandler) and sends the handler's Proxy, UseProxy, bypass list and credentials to the engine
