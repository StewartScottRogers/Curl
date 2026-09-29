---
id: BL-867
title: Count a kept Digest answer's nonce on to nc=00000002 when a proxy and an origin challenge in one transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-603]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-867 — Count a kept Digest answer's nonce on to nc=00000002 when a proxy and an origin challenge in one transfer

## Goal

When a transfer through a forward proxy answers both a `407` and a `401` with Digest, the request that carries the kept answer again sends it with the same cnonce, `nc=00000002` and the hash for that count, as curl 8.21.0 does.

## Context

- BL-603 Notes (cases `Uu-digest-407d-401d-200` and `Uu-any-401d-407d-200`) and ADR-0187: Curl resends the kept Digest value unchanged (`nc=00000001`), because `IHttpAuthenticator` keeps no state between calls (ADR-0014) and `DigestAuthenticator` hard-codes `NonceCount`.
- The fix needs a way for the HTTP handler to ask for the next use of an answer it already sent (a nonce count and the cnonce), in `Curl.Protocol.Abstractions` (`IHttpAuthenticator` or `HttpAuthRequest`), implemented in `Curl.Authentication.UnitLibrary`'s `DigestAuthenticator` and `RankedHttpAuthenticator`, and used by `HttpProtocolHandler.WithAuthorization` / `WithProxyAuthorization` for the header they keep.

## Acceptance criteria

- [ ] `HttpProtocolHandlerTests.ExecuteAsync_ProxyDigestThenOriginDigest_AnswersEachChallengeOnce` pins the third request's `Proxy-Authorization` with `cnonce="063231b54c58aa830f9917b0665bdaf8"`, `nc=00000002` and `response="924da41f0f75d705a8c76efb5ad7d596"` (curl's measured hash).
- [ ] `HttpProtocolHandlerTests.ExecuteAsync_OriginDigestThenProxyDigest_AnswersEachChallengeOnce` pins the third request's `Authorization` with `cnonce="f7604464c2453c62e1f5077435011686"`, `nc=00000002` and `response="19b392bfec0f8a5d87a9d959622271e4"`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
