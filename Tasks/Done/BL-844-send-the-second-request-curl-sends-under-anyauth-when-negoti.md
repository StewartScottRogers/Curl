---
id: BL-844
title: Send the second request curl sends under --anyauth when Negotiate makes no token
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-844 — Send the second request curl sends under --anyauth when Negotiate makes no token

## Goal

`--anyauth -u :` against a `401` offering only Negotiate, with no ticket, sends the second request curl 8.21.0 sends (without `Authorization`) and then ends on its 401 with exit 0, instead of ending on the first 401.

## Context

- Measured on Windows 2026-09-28 (ADR-0176, BL-527 Notes): the first request is a probe; after the 401 curl picks Negotiate, logs `Issue another request to this URL`, fails the context, sends the same request with no `Authorization`, and ends on that 401 with exit 0. Measure the Linux build too before pinning.
- Today `IHttpAuthenticator` returning `null` means "no retry" to `HttpProtocolHandler.RetryAuthorizationAsync`, so the handler cannot express "retry without a header". The task decides how the answer says so without looping (the second 401 must end the transfer).

## Acceptance criteria

- [x] A `Curl.Protocol.Http.UnitTests` test pins the two requests' bytes, the body and exit 0 for `--anyauth -u :` with a token source that makes no token.
- [x] `--negotiate -u :` alone still sends one request (the BL-527 tests stay green).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29 with `Record-CurlExchange.ps1`, `--anyauth -u : -v -s` against `401` + `WWW-Authenticate: Negotiate` + `Content-Length: 4` + `deny`, no ticket: curl 8.21.0 Schannel (Windows) and curl 8.18.0 OpenSSL + MIT Kerberos (Linux, WSL, via a `wsl.exe -e curl` wrapper passed as `-Curl`, server on `-ListenAddress 0.0.0.0`) behave the same apart from the failure line's words. Two requests, neither with `Authorization`, the second on the reused connection; no failure line after the first 401; `Issue another request`, `Reusing existing`, the failure and `Server auth using Negotiate with user ''` before the second; the failure again before the second 401's `WWW-Authenticate`; stdout `deny`; exit 0. The full transcript is in ADR-0232.
- Decided in ADR-0232 (by Claude under Stewart's delegation): `IHttpAuthenticator.CreateAuthorizationAsync` answering a challenge with `string.Empty` means "send the request again without the header". `RankedHttpAuthenticator` gives it when Negotiate is picked after the challenge (not the one scheme allowed) and makes no token; on that request's 401 it steps a context without answering (the `-v` line) and answers `null`. The handler also takes an empty answer from `ContinueAuthorizationAsync` as `null`, so it cannot loop. No new type: the head formatter already sends nothing for an empty value.
- The handler now records what the authenticator reports while answering a 401 whose request did not pick Negotiate, and writes it before the retry (curl steps that context on the way out); a plan keeps its info lines across a fresh-connection resend, as curl steps a context per send.
- `touches` grew, per rule 3, with no task in Doing naming either: `Curl.Protocol.Abstractions.UnitLibrary` (doc comments on `IHttpAuthenticator` only: the empty answer is part of its contract) and `Documentation/Planning/Decisions` (ADR-0232 and its index line).
- Coverage: `Measure-CodeQuality.ps1 -Library` gives 100% line and branch and 0 failing members for `Curl.Authentication.UnitLibrary` and `Curl.Protocol.Http.UnitLibrary`; Abstractions changed only in comments.
- Follow-up filed: BL-959 - no authentication retry writes curl's `Issue another request` and reuse lines yet; the `-v` test here pins everything else and says so.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --anyauth -u : against a Negotiate-only 401 with no ticket sends curl's second request without Authorization and ends on its 401 with exit 0
