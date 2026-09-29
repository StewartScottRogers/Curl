---
id: BL-841
title: Send the second request curl sends under --anyauth when Negotiate makes no token
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-841 — Send the second request curl sends under --anyauth when Negotiate makes no token

## Goal

`--anyauth -u :` against a `401` offering only Negotiate, with no ticket, sends the second request curl 8.21.0 sends (without `Authorization`) and then ends on its 401 with exit 0, instead of ending on the first 401.

## Context

- Measured on Windows 2026-09-28 (ADR-0176, BL-527 Notes): the first request is a probe; after the 401 curl picks Negotiate, logs `Issue another request to this URL`, fails the context, sends the same request with no `Authorization`, and ends on that 401 with exit 0. Measure the Linux build too before pinning.
- Today `IHttpAuthenticator` returning `null` means "no retry" to `HttpProtocolHandler.RetryAuthorizationAsync`, so the handler cannot express "retry without a header". The task decides how the answer says so without looping (the second 401 must end the transfer).

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` test pins the two requests' bytes, the body and exit 0 for `--anyauth -u :` with a token source that makes no token.
- [ ] `--negotiate -u :` alone still sends one request (the BL-527 tests stay green).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
