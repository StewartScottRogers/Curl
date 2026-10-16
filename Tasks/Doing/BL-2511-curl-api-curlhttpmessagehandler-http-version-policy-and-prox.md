---
id: BL-2511
title: Curl.Api: CurlHttpMessageHandler HTTP version policy and proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2510]
touches: [Curl.Api/HttpMessageHandler/Versions, Curl.Api.UnitTests/HttpMessageHandler/Versions]
requirement: none
created: 2026-10-16
completed:
---
# BL-2511 — Curl.Api: CurlHttpMessageHandler HTTP version policy and proxy

## Goal

The handler honours `HttpRequestMessage.Version` and `VersionPolicy` (`RequestVersionOrLower`, `RequestVersionOrHigher`, `RequestVersionExact`) for HTTP/1.1, HTTP/2 and HTTP/3, and a `Proxy` and credentials set on the handler reach the engine.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `EasyHttpOptions.HttpVersion` and `EasyProxyOptions`. Where the policy cannot be met the handler throws `HttpRequestException` as `SocketsHttpHandler` does.

## Acceptance criteria

- [ ] Tests assert the option the fake engine receives for each version and policy, the failure when the policy cannot be met and the proxy settings (address, credentials, bypass list).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: e5b4693b 4272a6b9 5393a01b 8738c7f6 ae118507.
