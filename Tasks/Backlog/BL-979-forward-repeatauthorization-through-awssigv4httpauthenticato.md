---
id: BL-979
title: Forward RepeatAuthorization through AwsSigV4HttpAuthenticator so curl.exe counts a kept Digest nonce on
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-869]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-979 — Forward RepeatAuthorization through AwsSigV4HttpAuthenticator so curl.exe counts a kept Digest nonce on

## Goal

`curl.exe --proxy-digest -U u:p -u a:b --digest` against a 407 then a 401 sends the kept `Proxy-Authorization` with `nc=00000002` and a new hash, as curl 8.21.0 does, not the first answer unchanged.

## Context

- BL-869 and ADR-0246 added `IHttpAuthenticator.RepeatAuthorization`, a default interface method that returns the value as sent, and implemented it in `RankedHttpAuthenticator` / `DigestAuthenticator`.
- `Curl.Console/AwsSigV4HttpAuthenticator.cs` wraps the ranked authenticator in `CurlComposition` and forwards `CreateAuthorization`, `CreateAuthorizationAsync` and `ContinueAuthorizationAsync` to `otherSchemes`, but not `RepeatAuthorization`, so the default (value as sent) wins in production. `Curl.Console` was held by another lane when BL-869 ran.

## Acceptance criteria

- [ ] `AwsSigV4HttpAuthenticator.RepeatAuthorization` returns `otherSchemes.RepeatAuthorization(request, sentAuthorization)` when `request.AwsSigV4` is `null`, and the value as sent otherwise.
- [ ] `AwsSigV4HttpAuthenticatorTests` covers both branches (a fake inner authenticator that marks its answer), and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Renumbered from BL-972, which the RSA coefficient fix pushed first also holds.
