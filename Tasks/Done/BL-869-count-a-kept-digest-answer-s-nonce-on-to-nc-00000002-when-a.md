---
id: BL-869
title: Count a kept Digest answer's nonce on to nc=00000002 when a proxy and an origin challenge in one transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-603]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-869 — Count a kept Digest answer's nonce on to nc=00000002 when a proxy and an origin challenge in one transfer

## Goal

When a transfer through a forward proxy answers both a `407` and a `401` with Digest, the request that carries the kept answer again sends it with the same cnonce, `nc=00000002` and the hash for that count, as curl 8.21.0 does.

## Context

- BL-603 Notes (cases `Uu-digest-407d-401d-200` and `Uu-any-401d-407d-200`) and ADR-0187: Curl resends the kept Digest value unchanged (`nc=00000001`), because `IHttpAuthenticator` keeps no state between calls (ADR-0014) and `DigestAuthenticator` hard-codes `NonceCount`.
- The fix needs a way for the HTTP handler to ask for the next use of an answer it already sent (a nonce count and the cnonce), in `Curl.Protocol.Abstractions` (`IHttpAuthenticator` or `HttpAuthRequest`), implemented in `Curl.Authentication.UnitLibrary`'s `DigestAuthenticator` and `RankedHttpAuthenticator`, and used by `HttpProtocolHandler.WithAuthorization` / `WithProxyAuthorization` for the header they keep.

## Acceptance criteria

- [x] `HttpProtocolHandlerTests.ExecuteAsync_ProxyDigestThenOriginDigest_AnswersEachChallengeOnce` pins the third request's `Proxy-Authorization` with `cnonce="063231b54c58aa830f9917b0665bdaf8"`, `nc=00000002` and `response="924da41f0f75d705a8c76efb5ad7d596"` (curl's measured hash).
- [x] `HttpProtocolHandlerTests.ExecuteAsync_OriginDigestThenProxyDigest_AnswersEachChallengeOnce` pins the third request's `Authorization` with `cnonce="f7604464c2453c62e1f5077435011686"`, `nc=00000002` and `response="19b392bfec0f8a5d87a9d959622271e4"`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Design (ADR-0246, decided by Claude under Stewart's delegation): `IHttpAuthenticator.RepeatAuthorization(request, sentAuthorization)`, a default interface method giving the value as sent. `DigestAuthenticator` reads the sent value back (`DigestChallengeParameters.ReadPairs`, new, which `Read` now uses too), takes its `cnonce` and hex `nc`, and rebuilds the answer with `nc + 1`; `RankedHttpAuthenticator` hands every value to it. `HttpProtocolHandler.RepeatAuthorization` / `RepeatProxyAuthorization` feed the kept header into `WithProxyAuthorization` / `WithAuthorization`. The authenticator stays stateless (ADR-0014): the sent value is the state.
- Both measured hashes were checked independently in PowerShell (MD5 of `HA1:nonce:00000002:cnonce:auth:HA2`) before pinning.
- Touches: added `Documentation/Planning/Decisions` for ADR-0246 and the note in ADR-0187; no task in Doing names it.
- Follow-up: `Curl.Console`'s `AwsSigV4HttpAuthenticator` wraps the ranked authenticator and does not forward `RepeatAuthorization`, so `curl.exe` still sends the kept answer unchanged until BL-972 (Curl.Console was held by another Doing task).
- Measured: Abstractions 628, Authentication 688 (4 skipped), Http 1422 (2 skipped) tests pass; all three libraries 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A kept Digest answer is sent again counted on to nc=00000002 with the same cnonce and curl's hash, through a proxy and an origin in either order
